using System.ComponentModel;
using Sinalo.App.ViewModels;
using Sinalo.Application.Synchronization;

namespace Sinalo.App;

// The removal worker reads only a published flag and the queue's locked snapshot,
// never dependency properties or Dispatcher.Invoke while holding the file gate.
public sealed class CategoryOperationState : IDisposable
{
    private LibraryViewModel? _library;
    private SynchronizationQueue? _queue;
    private int _importing;

    public bool IsBusy => Volatile.Read(ref _importing) != 0
        || Volatile.Read(ref _queue)?.GetSnapshot().IsProcessing == true;

    // Bind/dispose on the operator UI thread; reads are safe on worker threads.
    public void Bind(LibraryViewModel library, SynchronizationQueue queue)
    {
        Dispose();
        _library = library;
        Volatile.Write(ref _importing, library.IsImporting ? 1 : 0);
        Volatile.Write(ref _queue, queue);
        library.PropertyChanged += LibraryChanged;
    }

    private void LibraryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is LibraryViewModel library && e.PropertyName == nameof(LibraryViewModel.IsImporting))
            Volatile.Write(ref _importing, library.IsImporting ? 1 : 0);
    }

    public void Dispose()
    {
        if (_library is not null) _library.PropertyChanged -= LibraryChanged;
        _library = null;
        Volatile.Write(ref _queue, null);
        Volatile.Write(ref _importing, 0);
    }
}
