using System.Buffers.Binary;
using System.Text;
using Sinalo.Application.Library;
using Sinalo.Application.Playback;

namespace Sinalo.Infrastructure;

public sealed class Mp4LibraryValidator(PlaybackActivityGate? activity = null) : IMediaClassifier, ILocalMediaValidator, ILocalMediaMetadataReader
{
    public bool Supports(string path) => string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase);
    public async Task ValidateAsync(string path, CancellationToken token = default)
    {
        if (!Supports(path)) throw new InvalidDataException("Nesta entrega a biblioteca aceita vídeos MP4, não áudio, imagens ou arquivos parciais.");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var header = new byte[16];
        var ftyp = false; var moov = false; var mdat = false;
        var boxes = 0;
        while (stream.Position < stream.Length)
        {
            token.ThrowIfCancellationRequested();
            if (activity is not null) await activity.WaitUntilIdleAsync(token);
            var start = stream.Position;
            if (stream.Length - start < 8 || ++boxes > 100000) throw new InvalidDataException("Estrutura MP4 incompleta ou inválida.");
            await stream.ReadExactlyAsync(header.AsMemory(0, 8), token);
            long size = BinaryPrimitives.ReadUInt32BigEndian(header);
            var type = Encoding.ASCII.GetString(header, 4, 4);
            var minimum = 8;
            if (size == 1)
            {
                await stream.ReadExactlyAsync(header.AsMemory(8, 8), token);
                var extended = BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8));
                if (extended > long.MaxValue) throw new InvalidDataException("Tamanho MP4 inválido.");
                size = (long)extended; minimum = 16;
            }
            if (size == 0) size = stream.Length - start;
            if (size < minimum || size > stream.Length - start) throw new InvalidDataException("O vídeo MP4 está truncado.");
            ftyp |= type == "ftyp" && size >= minimum + 8;
            moov |= type == "moov" && size > minimum;
            mdat |= type == "mdat" && size > minimum;
            stream.Position = start + size;
        }
        if (!ftyp || !moov || !mdat) throw new InvalidDataException("O arquivo não contém a estrutura de um vídeo MP4 completo.");
    }
    public Task<LocalMediaMetadata> ReadAsync(string path, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(new LocalMediaMetadata()); // Optional metadata: no global ffprobe dependency.
    }
}
