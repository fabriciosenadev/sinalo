using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sinalo.App;
using Sinalo.App.ViewModels;
using Sinalo.App.ReleaseNotes;
using Sinalo.Application.Configuration;
using Sinalo.Application.Synchronization;
using Sinalo.Infrastructure;
using Sinalo.Domain;
using Sinalo.Tests.Integration;
using Button=System.Windows.Controls.Button;
using TextBox=System.Windows.Controls.TextBox;
using ListBox=System.Windows.Controls.ListBox;

namespace Sinalo.Tests.EndToEnd;

[Collection(WpfInterfaceCollection.Name)]
public sealed class ExperienceLayoutTests
{
    private static readonly SourceConfiguration[] Sources=[
        new(ContentSource.Missions,"Informativo das Missões","https://example.test/m",AvailabilityPolicy.RollingSaturday,new(true,true,true)),
        new(ContentSource.ProvaiEVede,"Provai e Vede","https://example.test/p",AvailabilityPolicy.QuarterlyFull,new(false,false,false)),
        new(ContentSource.Health,"Minuto de Saúde","https://example.test/h",AvailabilityPolicy.RollingSaturday,new(false,true,true))];
    private static HomeViewModel Model()
    {
        var items=Enumerable.Range(1,36).Select(i=>new ContentItem(i.ToString(),(ContentSource)(i%3),i==1?"O sonho de Enoc — Parte 1 — um título longo para conferir a legibilidade":"Vídeo da programação "+i,new DateOnly(2026,8,1).AddDays(i),new Uri("https://example.test/"+i),[new(i.ToString(),new("https://example.test/a.mp4"),"a.mp4",null,null)],SyncState.Ready,i%4==0,@"C:\Videos\teste.mp4")).ToArray();
        var model=new HomeViewModel(new SaturdayWindowService(),new LocalSinaloPathService(),Sources,items,worshipTimer:new(new(),new Audio()));
        model.SelectProgram("Informativo das Missões");
        return model;
    }
    [Theory]
    [InlineData(false,1280,720,1.0)]
    [InlineData(true,1280,720,1.0)]
    [InlineData(false,1920,1080,1.0)]
    [InlineData(true,940,580,1.0)]
    [InlineData(false,1280,720,1.25)]
    [InlineData(true,1280,720,1.5)]
    public void Workspaces_RenderAndKeepOperationalActionsAccessible(bool dark,double width,double height,double scale)
    {
        RunSta(()=>
        {
            var home=Model();var window=new MainWindow{DataContext=home,Width=width,Height=height};
            Theme(window,dark);
            ((FrameworkElement)window.Content).LayoutTransform=new ScaleTransform(scale,scale);
            window.Show();window.Width=width;window.Height=height;Flush(window);
            var list=(ListBox)window.FindName("CatalogList");
            Assert.True(list.ActualHeight>60,$"Altura da lista: {list.ActualHeight}");
            Save(window,$"programs-{dark}-{width}-{scale}");
            var scrolling=Descendants(list).OfType<ScrollViewer>().First();
            var scrollbar=Descendants(list).OfType<System.Windows.Controls.Primitives.ScrollBar>().First(b=>b.Orientation==System.Windows.Controls.Orientation.Vertical && b.IsVisible);
            System.Windows.Controls.Primitives.ScrollBar.PageDownCommand.Execute(null,scrollbar);Flush(window);Assert.True(scrolling.VerticalOffset>0);
            scrolling.ScrollToTop();Flush(window);
            var track=(System.Windows.Controls.Primitives.Track)scrollbar.Template.FindName("PART_Track",scrollbar);
            track.Thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0,0){RoutedEvent=System.Windows.Controls.Primitives.Thumb.DragStartedEvent});
            track.Thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(0,60){RoutedEvent=System.Windows.Controls.Primitives.Thumb.DragDeltaEvent});
            Flush(window);Assert.True(scrolling.VerticalOffset>0);scrolling.ScrollToTop();Flush(window);
            home.SelectedCatalogItem=home.CatalogItems[0];Flush(window);
            Assert.True(((Border)window.FindName("CatalogDetails")).IsVisible);
            Save(window,$"details-{dark}-{width}-{scale}");
            Invoke(window,"CloseDetails_Click");Assert.False(home.ShowCatalogDetails);
            foreach(var name in new[]{"Informativo das Missões","Provai e Vede","Minuto de Saúde"}){home.SelectProgram(name);Flush(window);Assert.Equal(name,home.WorkspaceTitle);}
            Invoke(window,"AddLinkForProgram_Click");Assert.Equal(ContentSource.Health,home.SelectedLinkedVideoDestination?.Source);
            home.LinkedVideoUrl="https://youtu.be/123";home.SetInspectedLinkedVideo(new("123","Saúde que inspira",new("https://example.test/123"),new(2026,10,10),[new("137","140",1080,300000000)]));
            Flush(window);AssertButtonGeometry(window);Save(window,$"link-{dark}-{width}-{scale}");
            var link=Descendants(window).OfType<Sinalo.App.Views.LinkedVideoView>().Single();
            var enqueue=Descendants(link).OfType<Button>().Single(b=>Equals(b.Content,"Adicionar à fila"));
            Assert.True(enqueue.IsVisible);Assert.True(enqueue.ActualHeight>=36);Assert.True(enqueue.TransformToAncestor((Visual)window.Content).Transform(new Point(0,enqueue.ActualHeight)).Y<=((FrameworkElement)window.Content).ActualHeight+1);
            home.SelectTimerWorkspace();Flush(window);AssertButtonGeometry(window);Assert.False(((Grid)window.FindName("ProgramWorkspace")).IsVisible);Save(window,$"timer-{dark}-{width}-{scale}");
            var timer=Descendants(window).OfType<Sinalo.App.Views.TimerView>().Single();
            var start=Descendants(timer).OfType<Button>().First(b=>Equals(b.Content,"Iniciar"));start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Flush(window);Assert.NotEqual("Parado",home.Timer.StateLabel);
            home.SelectWorshipTimerWorkspace();home.WorshipTimer.PlaySelectedAudio();Flush(window);AssertButtonGeometry(window);Save(window,$"worship-{dark}-{width}-{scale}");
            Assert.Contains(Descendants(window).OfType<Button>(),b=>Equals(b.Content,"Parar áudio") && b.IsVisible);
            home.SelectRaffleWorkspace();home.Raffle.NameToAdd="Fabricio";home.Raffle.AddName();home.Raffle.RangeStart="1";home.Raffle.RangeEnd="10";home.Raffle.AddRange();Flush(window);AssertButtonGeometry(window);Save(window,$"raffle-{dark}-{width}-{scale}");Assert.True(home.Raffle.CanStart);
            window.Close();
        });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SettingsAndDialogs_UseTheSameThemeAndProtectLegacyRules(bool dark)
    {
        RunSta(()=>
        {
            var config=new Config();var settings=new SettingsWindow(config){Width=680,Height=560};
            Theme(settings,dark);settings.Show();Flush(settings);
            var tabs=(TabControl)settings.FindName("SettingsCategories");
            for(var i=0;i<3;i++){tabs.SelectedIndex=i;Flush(settings);Assert.True(((Button)settings.FindName("SaveSettingsButton")).IsVisible);Save(settings,$"settings-{dark}-{i}");}
            Assert.True(((System.Windows.Controls.CheckBox)settings.FindName("MissionsCurrentSaturday")).IsChecked);
            Assert.True(((System.Windows.Controls.RadioButton)settings.FindName("ProvaiQuarterMode")).IsChecked);
            ((System.Windows.Controls.RadioButton)settings.FindName("ProvaiWeeklyMode")).IsChecked=true;
            Assert.True(((System.Windows.Controls.CheckBox)settings.FindName("ProvaiCurrentSaturday")).IsChecked);
            ((TextBox)settings.FindName("HealthUrl")).Text="inválida";
            Invoke(settings,"Save_Click");Assert.False(config.Saved);Assert.Contains("http",((TextBlock)settings.FindName("SettingsFeedback")).Text);
            ((TextBox)settings.FindName("HealthUrl")).Text="https://example.test/h";
            ((TextBox)settings.FindName("CleanupGracePeriodDays")).Text="999";Invoke(settings,"Save_Click");Assert.False(config.Saved);
            ((TextBox)settings.FindName("CleanupGracePeriodDays")).Text="30";
            typeof(SettingsWindow).GetField("_initialSettings",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(settings,typeof(SettingsWindow).GetMethod("CaptureSettings",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(settings,[]));
            settings.Close();
            var main=new MainWindow{DataContext=Model()};Theme(main,dark);main.Show();Flush(main);
            foreach(var dialog in new Window[]{new DownloadsWindow(main),new SessionScheduleWindow(main),new UpdateStatusWindow(main),new ReleaseNotesWindow(null)})
            {Theme(dialog,dark);dialog.Show();Flush(dialog);Save(dialog,$"dialog-{dialog.GetType().Name}-{dark}");dialog.Close();}
            var item=new ContentItem("x",ContentSource.Health,"Vídeo disponível",new(2026,8,8),new("https://example.test"),[new("x",new("https://example.test/a.mp4"),"a.mp4",null,null)]);
            var selection=new ManualVideoSelectionWindow("Minuto de Saúde",[new(item,true)],null);Theme(selection,dark);selection.Show();Flush(selection);Save(selection,$"selection-{dark}");
            Assert.Single(selection.SelectedItemIds);
            Descendants(selection).OfType<Button>().Single(b=>Equals(b.Content,"Limpar seleção")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.Empty(selection.SelectedItemIds);
            Descendants(selection).OfType<Button>().Single(b=>Equals(b.Content,"Selecionar disponíveis")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.Single(selection.SelectedItemIds);selection.Close();
            var diagnostic=new SynchronizationDiagnosticWindow(SynchronizationFailureClassifier.Classify(new IOException("Falha de teste"),ContentSource.Health,"Minuto de Saúde","https://example.test",SynchronizationStage.Storage),null);
            Theme(diagnostic,dark);diagnostic.Show();Flush(diagnostic);Save(diagnostic,$"diagnostic-{dark}");diagnostic.Close();
            var empty=new ManualVideoSelectionWindow("Provai e Vede",[],null);Theme(empty,dark);empty.Show();Flush(empty);Assert.Empty(empty.SelectedItemIds);empty.Close();
            main.Close();
        });
    }
    [Fact]
    public void ToolViews_ForwardCommandsAndRemainSafeWhenDetached()
    {
        RunSta(()=>
        {
            var audio=new Audio();
            var home=new HomeViewModel(new SaturdayWindowService(),new LocalSinaloPathService(),Sources,[],worshipTimer:new(new(),audio));
            var main=new MainWindow{DataContext=home,WorshipTimerAudioPlayer=audio};Theme(main,true);main.Show();Flush(main);
            foreach(var view in new FrameworkElement[]{new Sinalo.App.Views.TimerView(),new Sinalo.App.Views.RaffleView(),new Sinalo.App.Views.WorshipTimerView(),new Sinalo.App.Views.LinkedVideoView()})
                ForwardHandlers(view);
            foreach(var view in Descendants(main).OfType<System.Windows.Controls.UserControl>()) ForwardHandlers(view);
            home.SelectWorshipTimerWorkspace();home.WorshipTimer.PlaySelectedAudio();Flush(main);
            Assert.True(home.WorshipTimer.CanControlAudio);
            Assert.Contains(Descendants(main).OfType<Button>(),b=>Equals(b.Content,"Parar áudio") && b.IsVisible && b.IsEnabled);
            home.SelectTimerWorkspace();Flush(main);
            Descendants(main).OfType<Button>().First(b=>Equals(b.Content,"Parar áudio") && b.IsVisible).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.False(home.WorshipTimer.CanControlAudio);
            var options=Descendants(main).OfType<Button>().First(b=>Equals(b.Content,"Mais opções ▾"));
            options.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.True(options.ContextMenu.IsOpen);
            Flush(main);
            Assert.NotNull(options.ContextMenu.Template);
            var rule = options.ContextMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Header,"Alterar regra de busca"));
            Assert.Equal("ProgramSearchRules", rule.Tag);
            Assert.NotNull(rule.Template);
            options.ContextMenu.IsOpen=false;
            main.Close();
        });
    }
    [Fact]
    public void SearchRulesShortcut_HidesAppearanceAndStorageWhileFullSettingsKeepThem()
    {
        RunSta(() =>
        {
            var full = new SettingsWindow(new Config());
            var fullTabs = (TabControl)full.FindName("SettingsCategories");
            Assert.All(fullTabs.Items.OfType<TabItem>(), item => Assert.Equal(Visibility.Visible,item.Visibility));
            full.Close();
            var rules = new SettingsWindow(new Config());
            rules.ShowProgramSettings();
            var tabs = (TabControl)rules.FindName("SettingsCategories");
            Assert.Equal(2,tabs.SelectedIndex);
            Assert.Equal(Visibility.Collapsed,((TabItem)tabs.Items[0]).Visibility);
            Assert.Equal(Visibility.Collapsed,((TabItem)tabs.Items[1]).Visibility);
            Assert.Equal(Visibility.Visible,((TabItem)tabs.Items[2]).Visibility);
            Assert.Equal("Regras de busca dos programas",rules.Title);
            rules.Close();
        });
    }
    private static void ForwardHandlers(FrameworkElement view)
    {
        foreach(var method in view.GetType().GetMethods(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(m=>!m.Name.StartsWith('<') && m.GetParameters().Length==2 && typeof(RoutedEventArgs).IsAssignableFrom(m.GetParameters()[1].ParameterType)))
        {
            if(method.Name=="OpenDownloads_Click") continue;
            var type=method.GetParameters()[1].ParameterType;
            RoutedEventArgs args=type==typeof(System.Windows.Controls.SelectionChangedEventArgs)?new System.Windows.Controls.SelectionChangedEventArgs(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,Array.Empty<object>(),Array.Empty<object>()):type==typeof(RoutedPropertyChangedEventArgs<double>)?new RoutedPropertyChangedEventArgs<double>(0,0):new RoutedEventArgs();
            method.Invoke(view,[new Button{Tag="name"},args]);
        }
    }
    [Fact]
    public void Settings_ValidateFieldsAndExplicitModesWithoutLosingValues()
    {
        RunSta(()=>
        {
            var config=new Config();var window=new SettingsWindow(config);Theme(window,false);window.Show();Flush(window);
            bool Validate()=>(bool)typeof(SettingsWindow).GetMethod("ValidateSettings",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[])!;
            var url=(TextBox)window.FindName("MissionsUrl");
            foreach(var invalid in new[]{"ftp://example.test","não é endereço"}){url.Text=invalid;Assert.False(Validate());}
            url.Text="";Assert.True(Validate());url.Text="http://example.test";Assert.True(Validate());
            var days=(TextBox)window.FindName("CleanupGracePeriodDays");
            foreach(var invalid in new[]{"-1","181","abc"}){days.Text=invalid;Assert.False(Validate());}days.Text="0";Assert.True(Validate());days.Text="180";Assert.True(Validate());
            var path=(TextBox)window.FindName("ContentPathText");var original=path.Text;path.Text="";Assert.False(Validate());path.Text=original;
            foreach(var prefix in new[]{"Missions","Provai","Health"})
            {
                ((System.Windows.Controls.RadioButton)window.FindName(prefix+"QuarterMode")).IsChecked=true;
                Assert.False(((System.Windows.Controls.CheckBox)window.FindName(prefix+"CurrentSaturday")).IsChecked);
                ((System.Windows.Controls.RadioButton)window.FindName(prefix+"WeeklyMode")).IsChecked=true;
                Assert.True(((System.Windows.Controls.CheckBox)window.FindName(prefix+"CurrentSaturday")).IsChecked);
                ((System.Windows.Controls.CheckBox)window.FindName(prefix+"NextSaturday")).IsChecked=true;
                Assert.True(((System.Windows.Controls.RadioButton)window.FindName(prefix+"WeeklyMode")).IsChecked);
            }
            typeof(SettingsWindow).GetField("_initialSettings",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(window,typeof(SettingsWindow).GetMethod("CaptureSettings",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[]));window.Close();
        });
    }
    private sealed class Audio : IWorshipTimerAudioPlayer
    {
        public event EventHandler<WorshipTimerAudioSnapshot>? StateChanged;
        public WorshipTimerAudioSnapshot Snapshot{get;private set;}=WorshipTimerAudioSnapshot.Initial;
        private void Publish(WorshipTimerAudioPlaybackState state){Snapshot=Snapshot with{State=state};StateChanged?.Invoke(this,Snapshot);}
        public void Play(Sinalo.Application.WorshipTimer.WorshipTimerAudioCue cue,WorshipTimerAudioPlaybackOrigin origin){Snapshot=Snapshot with{Cue=cue,Origin=origin,Duration=TimeSpan.FromMinutes(2)};Publish(WorshipTimerAudioPlaybackState.Playing);}
        public void Pause()=>Publish(WorshipTimerAudioPlaybackState.Paused);
        public void Resume()=>Publish(WorshipTimerAudioPlaybackState.Playing);
        public void Stop()=>Publish(WorshipTimerAudioPlaybackState.Stopped);
        public void Seek(TimeSpan position){Snapshot=Snapshot with{Position=position};}
        public void SetVolume(double volume){Snapshot=Snapshot with{Volume=volume};}
        public void Refresh(){}
    }
    [Fact]
    public void Presentation_KeepsTheOpenedSceneAcrossNavigationAndExplainsMissingOutput()
    {
        RunSta(()=>
        {
            var output=new Sinalo.Application.Monitors.OutputProfile("screen","Tela principal",1,0,0,1024,768,true);
            var monitors=new Monitors(output);var presentation=new Presentation();var home=Model();
            var window=new MainWindow{DataContext=home,MonitorService=monitors,PresentationOutputService=presentation};Theme(window,false);window.Show();Flush(window);
            Invoke(window,"OpenRafflePresentation_Click");Assert.Equal("Sorteio",presentation.Scene?.Title);Assert.True(home.IsPresentationOpen);
            home.SelectTimerWorkspace();window.GetType().GetMethod("TimerRefresh_Tick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[window,EventArgs.Empty]);Assert.Equal("Sorteio",presentation.Scene?.Title);
            Invoke(window,"TestPresentation_Click");Assert.Equal("Sinalo",presentation.Scene?.Title);
            home.SelectWorshipTimerWorkspace();window.GetType().GetMethod("TimerRefresh_Tick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[window,EventArgs.Empty]);Assert.Equal("Sinalo",presentation.Scene?.Title);
            monitors.Outputs=[];Invoke(window,"OpenRafflePresentation_Click");Assert.Contains("não está disponível",home.OperationMessage);
            home.SelectedPlaybackScreen=null;Invoke(window,"OpenRafflePresentation_Click");
            var projected=new PresentationWindow{Width=1024,Height=768,DataContext=new Sinalo.Application.Presentation.PresentationScene("Sorteio","Um nome de participante muito longo para conferir a quebra e adaptação da apresentação","Vencedor")};
            projected.Show();Flush(projected);Save(projected,"presentation-long-name");Assert.False(((TextBlock)projected.FindName("EscapeInstruction")).IsVisible);projected.Close();window.Close();
        });
    }
    private sealed class Monitors(params Sinalo.Application.Monitors.OutputProfile[] outputs):Sinalo.Application.Monitors.IMonitorService
    {
        public IReadOnlyList<Sinalo.Application.Monitors.OutputProfile> Outputs{get;set;}=outputs;
        public Task<IReadOnlyList<Sinalo.Application.Monitors.OutputProfile>> GetOutputsAsync(CancellationToken cancellationToken=default)=>Task.FromResult(Outputs);
    }
    private sealed class Presentation:Sinalo.Application.Presentation.IPresentationOutputService
    {
        public bool IsOpen{get;private set;}
        public Sinalo.Application.Presentation.PresentationScene? Scene{get;private set;}
        public Task<Sinalo.Application.Presentation.PresentationOutputResult> ShowAsync(Sinalo.Application.Presentation.PresentationScene scene,Sinalo.Application.Monitors.OutputProfile output,CancellationToken cancellationToken=default){IsOpen=true;Scene=scene;return Task.FromResult(new Sinalo.Application.Presentation.PresentationOutputResult(true,"Apresentação aberta"));}
        public Task UpdateAsync(Sinalo.Application.Presentation.PresentationScene scene,CancellationToken cancellationToken=default){Scene=scene;return Task.CompletedTask;}
        public Task CloseAsync(CancellationToken cancellationToken=default){IsOpen=false;return Task.CompletedTask;}
    }
    private static void Theme(Window window,bool dark)
    {
        window.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Sinalo.App;component/VisualIdentity.xaml",UriKind.Relative)});
        SystemThemeService.ApplyToResources(window.Resources,dark);
        window.SetResourceReference(Window.StyleProperty,typeof(Window));
        window.SetResourceReference(Window.BackgroundProperty,"Brush.Window");
        window.SetResourceReference(Window.ForegroundProperty,"Brush.TextPrimary");
    }
    private static void Flush(Window window){window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);window.UpdateLayout();}
    private static void Invoke(object target,string method)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,[target,new RoutedEventArgs()]);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root){yield return root;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static void Save(Window window,string name)
    {
        var element=window;var bitmap=new RenderTargetBitmap(Math.Max(1,(int)element.ActualWidth),Math.Max(1,(int)element.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(element);
        var dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../TestResults/ux-ui"));Directory.CreateDirectory(dir);
        using var stream=File.Create(Path.Combine(dir,name+".png"));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));encoder.Save(stream);
    }
    private static void AssertButtonGeometry(FrameworkElement root)
    {
        foreach(var panel in Descendants(root).OfType<WrapPanel>().Where(panel => panel.IsVisible))
        {
            Button? previous = null;
            foreach(var button in panel.Children.OfType<Button>().Where(button => button.IsVisible))
            {
                Assert.Equal(40,button.ActualHeight,1);
                if(previous is not null)
                {
                    var before = previous.TranslatePoint(new Point(),panel);
                    var current = button.TranslatePoint(new Point(),panel);
                    if(current.X > before.X) Assert.Equal(before.Y,current.Y,1);
                }
                previous = button;
            }
        }
    }
    private static void RunSta(Action action)
    {
        Exception? error=null;var thread=new Thread(()=>{try{action();}catch(Exception caught){error=caught;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(30)),"Teste de UI não concluiu");Assert.Null(error);
    }
    private sealed class Config : ISinaloConfigurationService
    {
        public bool Saved{get;private set;}
        public Task<IReadOnlyList<SourceConfiguration>> LoadSourcesAsync(CancellationToken token=default)=>Task.FromResult<IReadOnlyList<SourceConfiguration>>(Sources);
        public Task SaveSourcesAsync(IReadOnlyList<SourceConfiguration> sources,CancellationToken token=default){Saved=true;return Task.CompletedTask;}
    }
}
