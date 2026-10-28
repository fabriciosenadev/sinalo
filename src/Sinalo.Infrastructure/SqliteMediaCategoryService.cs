using Microsoft.Data.Sqlite;
using Sinalo.Application.Library;
using Sinalo.Application.Playback;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class SqliteMediaCategoryService(ISinaloPathService paths, ILibraryRepository library,
    ContentOperationGate operations, PlaybackActivityGate playback, Func<bool>? busy = null) : IMediaCategoryService
{
    public event Action? Changed;
    public event Action<IReadOnlyList<string>>? MediaRemoved;
    private async Task<SqliteConnection> Open(CancellationToken token)
    {
        var c = new SqliteConnection($"Data Source={paths.GetPaths().DatabasePath};Foreign Keys=True");
        await c.OpenAsync(token); return c;
    }
    private static async Task<int> Execute(SqliteConnection c, string sql, CancellationToken token,
        SqliteTransaction? transaction = null, params (string, object?)[] values)
    {
        using var cmd = c.CreateCommand(); cmd.Transaction = transaction; cmd.CommandText = sql;
        foreach (var (key, value) in values) cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return await cmd.ExecuteNonQueryAsync(token);
    }
    public async Task<IReadOnlyList<MediaCategory>> ListAsync(CancellationToken token = default)
    {
        await using var c = await Open(token); using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,name,position,is_system,revision,deleting FROM media_categories ORDER BY is_system DESC,position,id";
        await using var r = await cmd.ExecuteReaderAsync(token); var result = new List<MediaCategory>();
        while (await r.ReadAsync(token)) result.Add(new(r.GetString(0),r.GetString(1),r.GetInt32(2),r.GetBoolean(3),r.GetInt64(4),r.GetBoolean(5)));
        return result;
    }
    public async Task ValidateDestinationAsync(string categoryId, CancellationToken token = default)
    {
        if (!(await ListAsync(token)).Any(x => x.Id == categoryId && !x.IsDeleting))
            throw new InvalidOperationException("A categoria foi removida ou está com exclusão pendente. Escolha outro destino.");
    }
    public async Task<MediaCategory> CreateAsync(string name, CancellationToken token = default)
    {
        var category = new MediaCategory("custom-" + Guid.NewGuid().ToString("N"), MediaCategory.ValidateName(name));
        await using var c = await Open(token);
        try { await Execute(c,"INSERT INTO media_categories(id,name,name_key,position) VALUES($id,$name,$key,COALESCE((SELECT MAX(position)+1 FROM media_categories),0))",token,null,
            ("$id",category.Id),("$name",category.Name),("$key",MediaCategory.NameKey(name))); }
        catch (SqliteException e) when (e.SqliteErrorCode == 19) { throw new ArgumentException("Já existe uma categoria com esse nome."); }
        Changed?.Invoke(); return (await ListAsync(token)).Single(x => x.Id == category.Id);
    }
    public async Task RenameAsync(MediaCategory category, string name, CancellationToken token = default)
    {
        var label = MediaCategory.ValidateName(name); await using var c = await Open(token);
        try
        {
            if (await Execute(c,"UPDATE media_categories SET name=$name,name_key=$key,revision=revision+1 WHERE id=$id AND revision=$revision AND is_system=0 AND deleting=0",token,null,
                ("$name",label),("$key",MediaCategory.NameKey(name)),("$id",category.Id),("$revision",category.Revision)) != 1)
                throw new InvalidOperationException("A categoria mudou ou não pode ser renomeada. Atualize a lista.");
        }
        catch (SqliteException e) when (e.SqliteErrorCode == 19) { throw new ArgumentException("Já existe uma categoria com esse nome."); }
        Changed?.Invoke();
    }
    public async Task ReorderAsync(string id, int direction, CancellationToken token = default)
    {
        var list = (await ListAsync(token)).Where(x => !x.IsSystem && !x.IsDeleting).ToList();
        var index = list.FindIndex(x => x.Id == id); var target = index + Math.Sign(direction);
        if (index < 0 || target < 0 || target >= list.Count) return;
        (list[index],list[target]) = (list[target],list[index]);
        await using var c = await Open(token); await using var tx = (SqliteTransaction)await c.BeginTransactionAsync(token);
        for (var i=0;i<list.Count;i++) await Execute(c,"UPDATE media_categories SET position=$position,revision=revision+1 WHERE id=$id AND deleting=0",token,tx,("$position",i+4),("$id",list[i].Id));
        await tx.CommitAsync(token); Changed?.Invoke();
    }
    public async Task AssignAsync(IReadOnlyList<string> mediaIds, string categoryId, CancellationToken token = default)
    {
        using var lease = await operations.EnterAsync(token); await ValidateDestinationAsync(categoryId,token);
        var items = new List<LibraryMedia>();
        foreach (var id in mediaIds.Distinct()) items.Add(await library.FindAsync(id,token) ?? throw new InvalidOperationException("Uma mídia foi removida. Atualize a lista."));
        var categories = await ListAsync(token);
        if (items.Any(x => categories.Any(cat => cat.Id==x.CategoryId && cat.IsDeleting)))
            throw new InvalidOperationException("Há mídia em exclusão pendente.");
        await using var c = await Open(token); await using var tx = (SqliteTransaction)await c.BeginTransactionAsync(token);
        foreach (var item in items) await Execute(c,"INSERT INTO media_category_assignments(media_id,category_id) VALUES($media,$category) ON CONFLICT(media_id) DO UPDATE SET category_id=excluded.category_id",token,tx,
            ("$media",item.ContentItemId ?? item.Id),("$category",categoryId));
        await tx.CommitAsync(token); Changed?.Invoke();
    }
    private Task<List<LibraryMedia>> Members(string id, CancellationToken token) => Task.Run(async () =>
    {
        var result = new List<LibraryMedia>();
        for (var page=0;;page++)
        {
            var batch = await library.QueryAsync(new(Page:page,PageSize:100,CategoryId:id,IncludeDetached:true),token);
            result.AddRange(batch.Items); if(result.Count>=batch.Total) return result;
        }
    },token);
    public async Task<CategoryImpact> InspectAsync(string categoryId, CancellationToken token = default)
    {
        var members=await Members(categoryId,token);
        return new(members.Count(x=>x.StorageMode==MediaStorageMode.Managed),members.Count(x=>x.StorageMode==MediaStorageMode.Referenced),
            members.Count(x=>x.IsPinned),members.Where(x=>x.StorageMode==MediaStorageMode.Managed).Sum(x=>x.SizeBytes ?? 0),members.Select(x=>x.Id).ToArray());
    }
    public Task RemoveAsync(MediaCategory category, string? transferTo, CancellationToken token = default, IReadOnlyList<string>? expectedMediaIds = null) => Task.Run(async () =>
    {
        if (category.IsSystem) throw new InvalidOperationException("As categorias de sistema não podem ser excluídas.");
        if (busy?.Invoke()==true) throw new InvalidOperationException("Conclua ou cancele os downloads/importações antes de excluir uma categoria.");
        using var lease = await operations.EnterAsync(token);
        if (busy?.Invoke()==true || (transferTo is null && playback.IsActive)) throw new InvalidOperationException("Encerre a reprodução e aguarde as operações pendentes antes de excluir.");
        await using var c = await Open(token);
        var current=(await ListAsync(token)).SingleOrDefault(x=>x.Id==category.Id) ?? throw new InvalidOperationException("A categoria já foi removida.");
        if(current.IsSystem)throw new InvalidOperationException("As categorias de sistema não podem ser excluídas.");
        if(current.Revision!=category.Revision) throw new InvalidOperationException("A categoria mudou. Confira a exclusão novamente.");
        var members=await Members(category.Id,token);
        if(expectedMediaIds is not null && !expectedMediaIds.ToHashSet(StringComparer.Ordinal).SetEquals(members.Select(x=>x.Id)))
            throw new InvalidOperationException("As mídias da categoria mudaram desde a confirmação. Confira novamente antes de continuar.");
        if(transferTo is not null)
        {
            if(current.IsDeleting || transferTo==category.Id) throw new InvalidOperationException("Escolha outra categoria; exclusão iniciada não pode virar transferência.");
            await ValidateDestinationAsync(transferTo,token);
            await using var tx=(SqliteTransaction)await c.BeginTransactionAsync(token);
            foreach(var item in members) await Execute(c,"INSERT INTO media_category_assignments(media_id,category_id) VALUES($id,$dest) ON CONFLICT(media_id) DO UPDATE SET category_id=excluded.category_id",token,tx,("$id",item.ContentItemId??item.Id),("$dest",transferTo));
            await Execute(c,"UPDATE media_category_assignments SET category_id=$dest WHERE category_id=$id",token,tx,("$dest",transferTo),("$id",category.Id));
            await Execute(c,"DELETE FROM media_categories WHERE id=$id",token,tx,("$id",category.Id));
            await tx.CommitAsync(token); Changed?.Invoke(); return;
        }
        if(!current.IsDeleting)
        {
            foreach(var item in members.Where(x=>x.StorageMode==MediaStorageMode.Managed && x.LocalPath is not null))
            {
                RequireOwned(item.LocalPath!);
                if(await library.CountPathReferencesAsync(item.LocalPath!,token)>1) throw new InvalidOperationException("Outro cadastro depende de um arquivo desta categoria. Remova o conflito antes de excluir.");
            }
            await using var tx=(SqliteTransaction)await c.BeginTransactionAsync(token);
            foreach(var item in members)
            {
                var f=item.LocalPath is null ? null : new FileInfo(item.LocalPath);
                await Execute(c,"INSERT INTO category_deletions(category_id,media_id,path,storage_mode,size,modified_ticks) VALUES($cat,$id,$path,$mode,$size,$ticks)",token,tx,
                    ("$cat",category.Id),("$id",item.Id),("$path",item.LocalPath),("$mode",(int)item.StorageMode),("$size",f?.Exists==true?f.Length:null),("$ticks",f?.Exists==true?f.LastWriteTimeUtc.Ticks:null));
            }
            await Execute(c,"UPDATE media_categories SET deleting=1 WHERE id=$id",token,tx,("$id",category.Id)); await tx.CommitAsync(token);
        }
        var removed=new List<string>();
        try
        {
            var jobs=new List<(string Id,string? Path,int Mode,long? Size,long? Ticks)>();
            using(var cmd=c.CreateCommand())
            {
                cmd.CommandText="SELECT media_id,path,storage_mode,size,modified_ticks FROM category_deletions WHERE category_id=$cat"; cmd.Parameters.AddWithValue("$cat",category.Id);
                await using var r=await cmd.ExecuteReaderAsync(token);
                while(await r.ReadAsync(token)) jobs.Add((r.GetString(0),r.IsDBNull(1)?null:r.GetString(1),r.GetInt32(2),r.IsDBNull(3)?null:r.GetInt64(3),r.IsDBNull(4)?null:r.GetInt64(4)));
            }
            foreach(var job in jobs)
            {
                token.ThrowIfCancellationRequested();
                var item=await library.FindAsync(job.Id,token);
                if(job.Mode==(int)MediaStorageMode.Managed && job.Path is { } path)
                {
                    RequireOwned(path);
                    if(File.Exists(path))
                    {
                        if(await library.CountPathReferencesAsync(path,token)>1) throw new InvalidOperationException("Arquivo compartilhado: a exclusão foi pausada.");
                        var f=new FileInfo(path);
                        if(job.Size!=f.Length || job.Ticks!=f.LastWriteTimeUtc.Ticks) throw new IOException("O arquivo mudou desde a confirmação; a exclusão foi pausada para proteger os dados.");
                        File.Delete(path);
                    }
                }
                await using var tx=(SqliteTransaction)await c.BeginTransactionAsync(token);
                var canonical=item?.ContentItemId ?? job.Id;
                await Execute(c,"DELETE FROM content_assets WHERE content_item_id=$canonical; DELETE FROM content_items WHERE id=$canonical; DELETE FROM media_category_assignments WHERE media_id=$canonical OR media_id=$id; DELETE FROM linked_library_media WHERE media_id=$id; DELETE FROM library_media WHERE id=$id OR content_item_id=$canonical; DELETE FROM category_deletions WHERE category_id=$cat AND media_id=$id",token,tx,
                    ("$canonical",canonical),("$id",job.Id),("$cat",category.Id));
                await tx.CommitAsync(token); removed.Add(canonical); if(canonical!=job.Id) removed.Add(job.Id);
            }
            await Execute(c,"DELETE FROM media_category_assignments WHERE category_id=$cat; DELETE FROM media_categories WHERE id=$cat",token,null,("$cat",category.Id));
        }
        finally { if(removed.Count>0) MediaRemoved?.Invoke(removed); Changed?.Invoke(); }
    },token);
    private void RequireOwned(string path)
    {
        var root=Path.GetFullPath(paths.GetPaths().ContentPath).TrimEnd('\\','/')+Path.DirectorySeparatorChar;
        var full=Path.GetFullPath(path);
        if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new IOException("Arquivo fora da área gerenciada: o original não será apagado.");
        for(string? part=full;part is not null && part.Length>=root.TrimEnd('\\').Length;part=Path.GetDirectoryName(part))
            if((File.Exists(part)||Directory.Exists(part)) && (File.GetAttributes(part)&FileAttributes.ReparsePoint)!=0) throw new IOException("Links de arquivos/pastas não podem ser seguidos na exclusão.");
    }
    public async Task<LibraryMedia?> FindLinkedAsync(string videoId,string categoryId,CancellationToken token=default)
        => await FindLinkedCoreAsync(videoId, categoryId, token);
    public async Task<LibraryMedia?> FindLinkedAnywhereAsync(string videoId,CancellationToken token=default)
        => await FindLinkedCoreAsync(videoId, null, token);
    private async Task<LibraryMedia?> FindLinkedCoreAsync(string videoId,string? categoryId,CancellationToken token)
    {
        await using var c=await Open(token);using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT m.id FROM library_media m LEFT JOIN content_items c ON c.id=m.content_item_id LEFT JOIN linked_library_media l ON l.media_id=m.id WHERE ($category IS NULL OR "+SqliteLibraryRepository.CategoryExpression+"=$category) AND (l.video_id=$video OR m.content_item_id IN($missions,$provai,$health)) ORDER BY m.id";
        cmd.Parameters.AddWithValue("$category",(object?)categoryId ?? DBNull.Value);cmd.Parameters.AddWithValue("$video",videoId);
        cmd.Parameters.AddWithValue("$missions","missions-youtube-"+videoId);cmd.Parameters.AddWithValue("$provai","provai-e-vede-youtube-"+videoId);cmd.Parameters.AddWithValue("$health","health-youtube-"+videoId);
        var ids=new List<string>();await using(var reader=await cmd.ExecuteReaderAsync(token))while(await reader.ReadAsync(token))ids.Add(reader.GetString(0));
        LibraryMedia? fallback=null;
        foreach(var id in ids){var item=await library.FindAsync(id,token);if(item?.CanPlay==true)return item;fallback??=item;}
        return fallback;
    }
    public async Task RegisterLinkedAsync(string mediaId,string videoId,CancellationToken token=default)
    {
        await using var c=await Open(token);
        await Execute(c,"INSERT INTO linked_library_media(media_id,video_id) VALUES($id,$video) ON CONFLICT(media_id) DO UPDATE SET video_id=excluded.video_id",token,null,("$id",mediaId),("$video",videoId));
    }
    public async Task PublishLinkedAsync(Sinalo.Application.Synchronization.LinkedVideoDownloadRequest request,ContentItem downloaded,CancellationToken token=default)
    {
        await ValidateDestinationAsync(request.EffectiveCategoryId,token);
        var asset=downloaded.Assets.Single();
        await using var c=await Open(token);await using var tx=(SqliteTransaction)await c.BeginTransactionAsync(token);
        await Execute(c,"UPDATE content_items SET local_path=$path,sync_state=3 WHERE id=(SELECT content_item_id FROM library_media WHERE id=$id); UPDATE content_assets SET expected_size_bytes=$size,sha256=$hash WHERE content_item_id=(SELECT content_item_id FROM library_media WHERE id=$id)",token,tx,("$path",downloaded.LocalPath),("$id",downloaded.Id),("$size",asset.ExpectedSizeBytes),("$hash",asset.Sha256));
        await Execute(c,"INSERT INTO library_media(id,display_name,usage_date,local_path,storage_mode,availability,added_at_utc,size_bytes,sha256,is_pinned) VALUES($id,$name,$date,$path,0,0,$time,$size,$hash,1) ON CONFLICT(id) DO UPDATE SET local_path=excluded.local_path,size_bytes=excluded.size_bytes,sha256=excluded.sha256,availability=0; INSERT INTO linked_library_media(media_id,video_id) VALUES($id,$video) ON CONFLICT(media_id) DO UPDATE SET video_id=excluded.video_id; INSERT INTO media_category_assignments(media_id,category_id) VALUES($id,$cat) ON CONFLICT(media_id) DO UPDATE SET category_id=excluded.category_id",token,tx,
            ("$id",downloaded.Id),("$name",downloaded.Title),("$date",request.CustomUsageDate?.ToString("yyyy-MM-dd")),("$path",downloaded.LocalPath),("$time",DateTimeOffset.UtcNow.ToString("O")),("$size",asset.ExpectedSizeBytes),("$hash",asset.Sha256),("$video",request.Video.Id),("$cat",request.EffectiveCategoryId));
        if (request.ChannelApproval is { } approval)
            await Execute(c,"UPDATE library_media SET published_date=COALESCE(published_date,$published),provider_channel_id=COALESCE(provider_channel_id,$channel) WHERE id=$id",token,tx,
                ("$published",approval.Candidate.Video.PublishedDate.ToString("yyyy-MM-dd")),("$channel",approval.Candidate.ChannelId),("$id",downloaded.Id));
        await tx.CommitAsync(token);Changed?.Invoke();
    }
}
