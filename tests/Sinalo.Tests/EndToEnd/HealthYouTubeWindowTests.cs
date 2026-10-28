using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Sinalo.App;
using Sinalo.App.ViewModels;
using Sinalo.Application.Synchronization;
using Sinalo.Infrastructure;
using static Sinalo.Tests.HealthYouTubeTestData;
using Button = System.Windows.Controls.Button;

namespace Sinalo.Tests.EndToEnd;
[Collection(WpfInterfaceCollection.Name)]
public sealed class HealthYouTubeWindowTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PendingQuery_HasVisibleProgressAndCannotApprove_ThenRestoresResultsOrError(bool dark, bool fail)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            HealthYouTubeWindow? window = null;
            try
            {
                var channel = new PendingChannel();
                var model = new HealthYouTubeViewModel(Configuration, channel, new Categories());
                window = new HealthYouTubeWindow(model, null) { Width = 620, Height = 550 };
                window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Sinalo.App;component/VisualIdentity.xaml", UriKind.Relative) });
                SystemThemeService.ApplyToResources(window.Resources, dark);
                window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, async () =>
                {
                    try
                    {
                        window.UpdateLayout();
                        Assert.True(model.IsBusy); Assert.False(model.CanApprove); Assert.False(model.HasResults); Assert.False(model.ShowEmptyState);
                        Assert.Contains("Busca em andamento", model.Summary);
                        var progress = (ProgressBar)window.FindName("SearchProgress");
                        Assert.True(progress.IsVisible); Assert.True(progress.IsIndeterminate);
                        Assert.False(((Button)window.FindName("RefreshVideosButton")).IsEnabled);
                        Assert.False(((Button)window.FindName("ApproveVideosButton")).IsEnabled);
                        Save(window, dark ? "loading-dark" : "loading-light");
                        var finished = new TaskCompletionSource();
                        model.PropertyChanged += (_, change) => { if (change.PropertyName == nameof(model.IsBusy) && !model.IsBusy) finished.TrySetResult(); };
                        if (fail) channel.Result.SetException(new System.IO.IOException("Rede indisponível"));
                        else channel.Result.SetResult(await new Channel().ListRecentAsync(Configuration));
                        await finished.Task;
                        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                        Assert.False(progress.IsIndeterminate); Assert.False(progress.IsVisible);
                        Assert.True(((Button)window.FindName("RefreshVideosButton")).IsEnabled);
                        Assert.False(model.CanApprove);
                        Assert.Equal(!fail, model.HasResults); Assert.Equal(fail, model.ShowEmptyState);
                        if (fail) Assert.Contains("Não foi possível", model.Message);
                    }
                    catch (Exception caught) { error = caught; }
                    finally { window.Close(); }
                });
                window.ShowDialog();
                Assert.Empty(window.ApprovedRequests);
            }
            catch (Exception caught) { error = caught; window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        Assert.Null(error);
    }
    private sealed class PendingChannel : IChannelVideoService
    {
        public TaskCompletionSource<ChannelVideoQuery> Result { get; } = new();
        public Task<ChannelVideoQuery> ListRecentAsync(HealthYouTubeConfiguration configuration, CancellationToken token = default) => Result.Task.WaitAsync(token);
        public Task<YouTubeChannelIdentity> VerifyAsync(string url, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ChannelVideo?> InspectAsync(string id, HealthYouTubeConfiguration configuration, CancellationToken token = default) => throw new NotSupportedException();
    }
    private static void Save(Window window, string name)
    {
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(window);
        var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../TestResults/health-channel"));
        System.IO.Directory.CreateDirectory(folder);
        using var output = System.IO.File.Create(System.IO.Path.Combine(folder, name + ".png"));
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); encoder.Save(output);
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SelectionWindow_RequiresExplicitSelectionAndConfirmationAndHasVisibleFooter(bool approve, bool dark)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            HealthYouTubeWindow? window = null;
            try
            {
                var channel = new Channel { Videos = [Video(1) with { Video = Video(1).Video with { Title = "EP 131 — O perigo escondido e os cuidados para uma vida saudável | MOMENTO VIDA E SAÚDE" } }, Video(2), Video(3), Video(4)] };
                var model = new HealthYouTubeViewModel(Configuration, channel, new Categories());
                window = new HealthYouTubeWindow(model, null) { Width = 620, Height = 550 };
                window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Sinalo.App;component/VisualIdentity.xaml", UriKind.Relative) });
                SystemThemeService.ApplyToResources(window.Resources, dark);
                window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
                {
                    try
                    {
                        window.UpdateLayout();
                        Assert.Equal(4, model.Items.Count);
                        Assert.All(model.Items, x => Assert.False(x.IsSelected));
                        var confirm = Descendants(window).OfType<Button>().Single(b => b.Content?.ToString()?.StartsWith("Baixar selecionados") == true);
                        Assert.False(confirm.IsEnabled);
                        var bounds = confirm.TransformToAncestor(window).TransformBounds(new Rect(confirm.RenderSize));
                        Assert.True(bounds.Bottom <= window.ActualHeight);
                        Assert.Equal(40, confirm.ActualHeight);
                        Assert.NotNull(window.Background);
                        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../TestResults/health-channel"));
                        System.IO.Directory.CreateDirectory(folder);
                        using (var output = System.IO.File.Create(System.IO.Path.Combine(folder, dark ? "dark.png" : "light.png")))
                        {
                            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); encoder.Save(output);
                        }
                        if (approve)
                        {
                            model.Items[0].IsSelected = true;
                            window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                            Assert.True(confirm.IsEnabled);
                            confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }
                        else window.Close();
                    }
                    catch (Exception caught) { error = caught; window.Close(); }
                });
                var confirmed = window.ShowDialog();
                if (approve && error is null) { Assert.True(confirmed); Assert.Equal(Id(1), Assert.Single(window.ApprovedRequests).Video.Id); }
                else Assert.Empty(window.ApprovedRequests);
            }
            catch (Exception caught) { error = caught; window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        Assert.Null(error);
    }
    [Fact]
    public void ChannelAction_OnlyAppearsInHealthAndDoesNotReplaceTheSiteAction()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var model = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), Sinalo.Application.Configuration.OfficialContentPrograms.All);
                var window = new MainWindow { DataContext = model };
                window.Show();
                model.SelectProgram("Minuto de Saúde"); window.UpdateLayout();
                Assert.True(((Button)window.FindName("HealthYouTubeButton")).IsVisible);
                model.SelectProgram("Provai e Vede"); window.UpdateLayout();
                Assert.False(((Button)window.FindName("HealthYouTubeButton")).IsVisible);
                window.Close();
            }
            catch (Exception caught) { error = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        Assert.Null(error);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
