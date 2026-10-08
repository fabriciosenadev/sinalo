using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using Sinalo.Application.Monitors;
using Sinalo.Application.Presentation;

namespace Sinalo.App;

public interface IPresentationWindowHost
{
    bool IsVisible { get; }
    void Display(PresentationScene scene, OutputProfile output);
    void Close();
}

public interface IPresentationWindowFactory
{
    IPresentationWindowHost Create();
}

public sealed class PresentationWindowFactory : IPresentationWindowFactory
{
    public IPresentationWindowHost Create() => new PresentationWindow();
}

public partial class PresentationWindow : Window, IPresentationWindowHost
{
    private OutputProfile? _output;

    public PresentationWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => PositionOnOutput();
        SizeChanged += (_,_) => PresentationSafeArea.Margin = new Thickness(Math.Clamp(ActualWidth * 0.04, 24, 80));
    }

    public void Display(PresentationScene scene, OutputProfile output)
    {
        DataContext = scene;
        EscapeInstruction.Visibility = scene.Title == "Sinalo" ? Visibility.Visible : Visibility.Collapsed;
        _output = output;
        if (!IsVisible) Show();
        PositionOnOutput();
        Activate();
    }

    private void PresentationWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Close();
    }

    private void PositionOnOutput()
    {
        if (_output is null) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;

        _ = SetWindowPos(handle, new IntPtr(-1), _output.BoundsX, _output.BoundsY, _output.BoundsWidth, _output.BoundsHeight, 0x0040);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr windowHandle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
