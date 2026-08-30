using Serilog;

namespace Sundex.Engine;

/// <summary>
///     Watches the markup, stylesheets and logic scripts in the source tree and raises a
///     <see cref="HotReload" /> request when one is saved. Such an edit produces no IL delta,
///     so the IDE's hot-reload button has nothing to apply and never reaches
///     <see cref="HotReload.UpdateApplication" /> for it.
///     <para>
///         Only ever constructed against <see cref="Asset_Management.AssetProvider.SourceRoots" />,
///         which is empty outside Debug - so in Release this watches nothing and starts no
///         threads.
///     </para>
/// </summary>
public sealed class SourceWatcher : IDisposable
{
    /// <summary>
    ///     How long to wait for the writes to stop before reloading. Editors save through a
    ///     temp file and a rename, so one Ctrl+S is several events; a save-all across a
    ///     handful of files should also be one reload rather than a handful of rebuilds.
    /// </summary>
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(250);

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly ILogger _logger;
    private readonly Lock _lock = new();

    private Timer? _timer;
    private ReloadScope _pending = ReloadScope.Styles;

    public SourceWatcher(ILogger logger, string[] sourceRoots)
    {
        _logger = logger.ForContext<SourceWatcher>();

        foreach (var root in sourceRoots)
        {
            var watcher = new FileSystemWatcher(root)
            {
                Filter = "*.snx.*",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Renamed += OnChanged;
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }

        if (_watchers.Count > 0)
            _logger.Debug("[Hot Reload] Watching {Count} source root(s) for UI changes", _watchers.Count);
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();

        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        // A stylesheet alone can be applied to the tree that is already up; markup and
        // logic cannot. When a batch mixes the two the wider scope wins, so saving a
        // layout and its sheet together still rebuilds once.
        var scope = e.Name?.EndsWith(".snx.ss", StringComparison.OrdinalIgnoreCase) == true
            ? ReloadScope.Styles
            : ReloadScope.Full;

        lock (_lock)
        {
            if (_timer is null) _pending = scope;
            else if (scope == ReloadScope.Full) _pending = ReloadScope.Full;

            _timer?.Dispose();
            _timer = new Timer(Fire, null, Debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void Fire(object? state)
    {
        ReloadScope scope;
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            scope = _pending;
        }

        _logger.Debug("[Hot Reload] Source changed, requesting a {Scope} reload", scope);
        HotReload.Request(scope);
    }
}
