using Sinalo.App.ViewModels;
using Sinalo.Application.Configuration;
using Sinalo.Application.Storage;
using Sinalo.Infrastructure;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

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
        var viewModel = new HomeViewModel(new SaturdayWindowService(), pathService,
        [new(Sinalo.Domain.ContentSource.Missions, "Informativo das Missões", "https://missions.example/", Sinalo.Domain.AvailabilityPolicy.MonthlyFull), new(Sinalo.Domain.ContentSource.ProvaiEVede, "Provai e Vede", "", Sinalo.Domain.AvailabilityPolicy.QuarterlyFull), new(Sinalo.Domain.ContentSource.Health, "Minuto de Saúde", "", Sinalo.Domain.AvailabilityPolicy.MonthlyFull)]);

        Assert.Equal(3, viewModel.Sources.Count);
        Assert.Contains("Informativo", viewModel.Sources[0].Name);
        Assert.Equal("Fonte configurada", viewModel.Sources[0].Status);
        Assert.Equal("Configuração da fonte pendente", viewModel.Sources[1].Status);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.PreviousSaturday));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.CurrentSaturday));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.NextSaturday));
        Assert.EndsWith("Sinalo\\content", viewModel.ContentPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsWorkflow_ShouldLoadAndSaveTheThreeConfiguredUrls()
    {
        Exception? exception = null;
        var service = new FakeConfigurationService();
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Sinalo.App.SettingsWindow(service);
                window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(() =>
                {
                    ((TextBox)window.FindName("MissionsUrl")).Text = "https://missions.example/";
                    ((TextBox)window.FindName("ProvaiUrl")).Text = "https://provai.example/";
                    ((TextBox)window.FindName("HealthUrl")).Text = "https://health.example/";
                    typeof(Sinalo.App.SettingsWindow)
                        .GetMethod("Save_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(window, [window, new RoutedEventArgs()]);
                });

                window.ShowDialog();
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
        Assert.True(service.WasSaved);
        Assert.Equal("https://missions.example/", service.SavedSources.Single(source => source.Source == Sinalo.Domain.ContentSource.Missions).PageUrl);
        Assert.Equal("https://provai.example/", service.SavedSources.Single(source => source.Source == Sinalo.Domain.ContentSource.ProvaiEVede).PageUrl);
        Assert.Equal("https://health.example/", service.SavedSources.Single(source => source.Source == Sinalo.Domain.ContentSource.Health).PageUrl);
    }

    private sealed class FakeConfigurationService : ISinaloConfigurationService
    {
        public bool WasSaved { get; private set; }
        public IReadOnlyList<SourceConfiguration> SavedSources { get; private set; } = [];

        public Task<IReadOnlyList<SourceConfiguration>> LoadSourcesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceConfiguration>>(
            [
                new(Sinalo.Domain.ContentSource.Missions, "Informativo das Missões", "", Sinalo.Domain.AvailabilityPolicy.MonthlyFull),
                new(Sinalo.Domain.ContentSource.ProvaiEVede, "Provai e Vede", "", Sinalo.Domain.AvailabilityPolicy.QuarterlyFull),
                new(Sinalo.Domain.ContentSource.Health, "Minuto de Saúde", "", Sinalo.Domain.AvailabilityPolicy.MonthlyFull)
            ]);

        public Task SaveSourcesAsync(IReadOnlyList<SourceConfiguration> sources, CancellationToken cancellationToken = default)
        {
            SavedSources = sources;
            WasSaved = true;
            return Task.CompletedTask;
        }
    }
}
