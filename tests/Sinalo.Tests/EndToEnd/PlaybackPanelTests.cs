using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Sinalo.App;
using Sinalo.App.ViewModels;
using Sinalo.Application.Playback;
using Sinalo.Infrastructure;
using Sinalo.Tests.Unit;

namespace Sinalo.Tests.EndToEnd;

public sealed class PlaybackPanelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ControlsKeepTheirSessionAcrossNavigationAndUseTheTheme(bool dark)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? window = null;
            try
            {
                var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
                var document = XDocument.Load(Path.Combine(root, "src/Sinalo.App/App.xaml"));
                XNamespace ui = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                var dictionary = new XElement(ui + "ResourceDictionary", new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"), document.Root!.Element(ui + "Application.Resources")!.Elements());
                var resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
                SystemThemeService.ApplyToResources(resources, dark);
                var controller = new RecordingPlaybackController();
                controller.Update(PlaybackControllerTests.Active());
                var panel = new PlaybackViewModel(controller, new PlaybackService(new Catalog(), controller));
                window = new MainWindow { Resources = resources, Playback = panel, DataContext = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), []) };
                window.Show();
                window.UpdateLayout();
                var host = (StackPanel)window.FindName("PlaybackPanel");
                var seek = (Slider)window.FindName("VideoSeekSlider");
                var volume = (Slider)window.FindName("VideoVolumeSlider");
                Assert.Same(panel, host.DataContext);
                Assert.True(seek.IsEnabled);
                Assert.Equal(60, seek.Maximum);
                Assert.Equal(12, seek.Value);
                var track = (Track)seek.Template.FindName("PART_Track", seek);
                Assert.True(track.Thumb.ActualWidth > 0);
                Assert.True(track.Thumb.ActualHeight > 0);
                var source = PresentationSource.FromVisual(window)!;
                seek.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Right) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                seek.Value = 25;
                controller.Update(controller.Current with { PositionSeconds = 18 });
                Assert.Equal(25, seek.Value);
                seek.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Right) { RoutedEvent = Keyboard.PreviewKeyUpEvent });
                Assert.Equal(25, controller.Sought);
                var count = controller.Commands.Count;
                seek.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Tab) { RoutedEvent = Keyboard.PreviewKeyUpEvent });
                Assert.Equal(count, controller.Commands.Count);
                window.DataContext = new HomeViewModel(new SaturdayWindowService(), new LocalSinaloPathService(), []);
                Assert.Same(panel, host.DataContext);
                controller.Update(controller.Current with { Capabilities = PlaybackCapabilities.None });
                Assert.False(seek.IsEnabled);
                Assert.False(volume.IsEnabled);
                Assert.Contains("externo", panel.Message);
                controller.Update(PlaybackControllerTests.Active());
                host.BringIntoView();
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var destination = Path.Combine(root, "TestResults", $"playback-panel-{(dark ? "dark" : "light")}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var output = File.Create(destination);
                encoder.Save(output);
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A interface não terminou a validação a tempo.");
        Assert.Null(failure);
    }

    private sealed class Catalog : Sinalo.Application.Catalog.IContentCatalog
    {
        public Task UpsertAsync(IReadOnlyList<Sinalo.Domain.ContentItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Sinalo.Domain.ContentItem>> ListBySourceAsync(Sinalo.Domain.ContentSource source, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Sinalo.Domain.ContentItem>>([]);
    }
}
