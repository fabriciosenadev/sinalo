using Sinalo.App.ViewModels;
using Sinalo.Application.Storage;
using Sinalo.Infrastructure;
using System.Reflection;
using System.Threading;

namespace Sinalo.Tests.EndToEnd;

public sealed class HomeWorkflowTests
{
    [Fact]
    public void MainWindow_ShouldLoadItsVisualTreeOnAnStaThread()
    {
        Exception? exception = null;
        string? title = null;

        var thread = new Thread(() =>
        {
            try
            {
                var window = new Sinalo.App.MainWindow();
                title = window.Title;

                var initializeComponent = typeof(Sinalo.App.MainWindow)
                    .GetMethod("InitializeComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                initializeComponent!.Invoke(window, null);

                window.Close();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
        Assert.Equal("Sinalo", title);
    }

    [Fact]
    public void HomeWorkflow_ShouldExposeOperationalDatesSourcesAndContentLocation()
    {
        var pathService = new LocalSinaloPathService();
        var viewModel = new HomeViewModel(new SaturdayWindowService(), pathService);

        Assert.Equal(3, viewModel.Sources.Count);
        Assert.Contains("Informativo", viewModel.Sources[0].Name);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.PreviousSaturday));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.CurrentSaturday));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.NextSaturday));
        Assert.EndsWith("Sinalo\\content", viewModel.ContentPath, StringComparison.OrdinalIgnoreCase);
    }
}
