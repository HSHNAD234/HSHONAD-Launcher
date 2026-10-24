using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HSHONAD_Launcher
{
    /// <summary>
    /// Owns the game process and ties proxy lifetime to the actual game process.
    /// Integrate into Form1 and call LaunchAsync with the selected executable path.
    /// </summary>
    public sealed class GameProcessManager : IAsyncDisposable
    {
        private readonly Func<Task> _startProxy;
        private readonly Func<Task> _stopProxy;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private Process? _game;
        private int _cleanupStarted;

        public bool IsGameRunning => _game is { HasExited: false };

        public GameProcessManager(Func<Task> startProxy, Func<Task> stopProxy)
        {
            _startProxy = startProxy ?? throw new ArgumentNullException(nameof(startProxy));
            _stopProxy = stopProxy ?? throw new ArgumentNullException(nameof(stopProxy));
        }

        public async Task LaunchAsync(string executablePath, string? arguments = null, string? workingDirectory = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new ArgumentException("Game executable path is required.", nameof(executablePath));
            if (!Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath))
                throw new FileNotFoundException("Selected game executable was not found.", executablePath);
            if (!string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Selected game file must be an .exe.", nameof(executablePath));

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (IsGameRunning)
                    throw new InvalidOperationException("The game is already running.");

                string cwd = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Path.GetDirectoryName(executablePath)!
                    : Path.GetFullPath(workingDirectory);

                await _startProxy().ConfigureAwait(false);
                try
                {
                    var start = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = arguments ?? string.Empty,
                        WorkingDirectory = cwd,
                        UseShellExecute = false
                    };

                    var process = new Process { StartInfo = start, EnableRaisingEvents = true };
                    process.Exited += GameExited;
                    if (!process.Start())
                    {
                        process.Exited -= GameExited;
                        process.Dispose();
                        throw new InvalidOperationException("Windows did not start the game process.");
                    }

                    _game = process;
                    _ = ObserveExitAsync(process);
                }
                catch
                {
                    await _stopProxy().ConfigureAwait(false);
                    throw;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private void GameExited(object? sender, EventArgs e)
        {
            if (sender is Process process)
                _ = CleanupAfterExitAsync(process);
        }

        private async Task ObserveExitAsync(Process process)
        {
            try { await process.WaitForExitAsync().ConfigureAwait(false); }
            catch (InvalidOperationException) { }
            await CleanupAfterExitAsync(process).ConfigureAwait(false);
        }

        private async Task CleanupAfterExitAsync(Process process)
        {
            if (Interlocked.Exchange(ref _cleanupStarted, 1) != 0)
                return;

            try
            {
                await _stopProxy().ConfigureAwait(false);
            }
            finally
            {
                await _gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (ReferenceEquals(_game, process))
                        _game = null;
                    process.Exited -= GameExited;
                    process.Dispose();
                    Interlocked.Exchange(ref _cleanupStarted, 0);
                }
                finally { _gate.Release(); }
            }
        }

        public async ValueTask DisposeAsync()
        {
            Process? process = _game;
            if (process is { HasExited: false })
            {
                try { process.CloseMainWindow(); }
                catch (InvalidOperationException) { }
                try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
                catch (TimeoutException)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) { }
                    try { await process.WaitForExitAsync().ConfigureAwait(false); }
                    catch (InvalidOperationException) { }
                }
            }

            if (process != null)
                await CleanupAfterExitAsync(process).ConfigureAwait(false);
            else
                await _stopProxy().ConfigureAwait(false);

            _gate.Dispose();
        }
    }
}