using System.Diagnostics;
using System.Net.Http;

namespace ScannerService.TrayApp;

public class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _statusCheckTimer;
    private readonly HttpClient _httpClient;
    private readonly SynchronizationContext _syncContext;

    private Process? _apiProcess;
    private ToolStripMenuItem? _statusItem;
    private ToolStripMenuItem? _startMenuItem;
    private ToolStripMenuItem? _stopMenuItem;

    private bool _isDisposed;
    private bool _isActuallyRunning;
    private readonly object _processLock = new object();
    private readonly object _stateLock = new object();

    // Configuration from .env
    private readonly string _apiExePath;
    private readonly string _apiHealthUrl;
    private readonly string _processName;
    private readonly int _statusCheckInterval;
    private readonly int _httpTimeout;
    private readonly int _startupDelay;
    private readonly int _apiPort;

    public TrayApp()
    {
        // Load .env file - try .development first, fallback to .env
        var envPath = Path.Combine(AppContext.BaseDirectory, ".env.development");
        if (!File.Exists(envPath))
            envPath = Path.Combine(AppContext.BaseDirectory, ".env");

        DotNetEnv.Env.Load(envPath);

        // Read configuration
        _apiExePath = DotNetEnv.Env.GetString("API_EXE_PATH");
        var port = DotNetEnv.Env.GetString("API_PORT", "5000");
        var host = DotNetEnv.Env.GetString("API_HOST", "localhost");
        _apiHealthUrl = $"http://{host}:{port}/api/health";
        _processName = DotNetEnv.Env.GetString("PROCESS_NAME", "ScannerService.Api");
        _statusCheckInterval = int.Parse(DotNetEnv.Env.GetString("STATUS_CHECK_INTERVAL", "5000"));
        _httpTimeout = int.Parse(DotNetEnv.Env.GetString("HTTP_TIMEOUT", "2000"));
        _startupDelay = int.Parse(DotNetEnv.Env.GetString("STARTUP_DELAY", "2000"));
        _apiPort = int.Parse(DotNetEnv.Env.GetString("API_PORT", "5000"));

        // Capture the UI synchronization context
        _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        // Initialize HttpClient (reusable)
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(_httpTimeout)
        };

        // Initialize tray icon
        _icon = new NotifyIcon
        {
            Icon = CreateIcon(false),
            Visible = true,
            Text = "Scanner Service - Stopped",
            ContextMenuStrip = CreateMenu()
        };

        // Initialize and start status check timer
        _statusCheckTimer = new System.Windows.Forms.Timer
        {
            Interval = _statusCheckInterval
        };
        _statusCheckTimer.Tick += OnStatusCheckTimerTick;
        _statusCheckTimer.Start();

        // Start the service on application launch
        StartService();
    }

    private ContextMenuStrip CreateMenu()
    {
        var menu = new ContextMenuStrip();

        // Status indicator (non-clickable)
        _statusItem = new ToolStripMenuItem
        {
            Text = "● Scanner is Stopped",
            Enabled = false,
            Font = new Font(menu.Font, FontStyle.Bold),
            ForeColor = Color.Red
        };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());

        // Start/Stop commands
        _startMenuItem = new ToolStripMenuItem("Start", null, (s, e) => StartService())
        {
            Name = "start"
        };
        _stopMenuItem = new ToolStripMenuItem("Stop", null, (s, e) => StopService())
        {
            Name = "stop"
        };

        menu.Items.Add(_startMenuItem);
        menu.Items.Add(_stopMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, OnExit));

        // Update menu items state when opening
        menu.Opening += OnMenuOpening;

        return menu;
    }

    private void OnMenuOpening(object? sender, EventArgs e)
    {
        bool running;
        lock (_stateLock)
        {
            running = _isActuallyRunning;
        }

        if (_startMenuItem != null)
            _startMenuItem.Enabled = !running;

        if (_stopMenuItem != null)
            _stopMenuItem.Enabled = running;
    }

    private void OnStatusCheckTimerTick(object? sender, EventArgs e)
    {
        // Fire and forget - don't await to prevent blocking the timer
        _ = CheckStatusAsync();
    }

    private async Task CheckStatusAsync()
    {
        if (_isDisposed) return;

        try
        {
            bool isResponding = await IsApiRespondingAsync();

            // Update the actual running state
            lock (_stateLock)
            {
                _isActuallyRunning = isResponding;
            }

            // Marshal UI update back to UI thread
            _syncContext.Post(_ =>
            {
                if (!_isDisposed)
                    UpdateUI(isResponding);
            }, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Status check failed: {ex.Message}");
        }
    }

    private void UpdateUI(bool running)
    {
        try
        {
            if (_statusItem != null)
            {
                _statusItem.Text = running ? $"● Scanner is Running - localhost:{_apiPort}" : "● Scanner is Stopped";
                _statusItem.ForeColor = running ? Color.Green : Color.Red;
            }

            if (!_isDisposed && _icon != null)
            {
                var oldIcon = _icon.Icon;
                _icon.Icon = CreateIcon(running);
                _icon.Text = running ? "Scanner Service - Running" : "Scanner Service - Stopped";

                // Dispose old icon to prevent resource leak
                oldIcon?.Dispose();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UI update failed: {ex.Message}");
        }
    }

    private void StartService()
    {
        if (_isDisposed) return;

        lock (_processLock)
        {
            if (IsProcessRunning())
            {
                ShowNotification("Already running", ToolTipIcon.Info);
                return;
            }

            try
            {
                if (!File.Exists(_apiExePath))
                {
                    MessageBox.Show(
                        $"Service executable not found:\n{_apiExePath}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                _apiProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _apiExePath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(_apiExePath) ?? AppContext.BaseDirectory,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false
                    },
                    EnableRaisingEvents = true
                };

                // Handle unexpected process exit
                _apiProcess.Exited += OnProcessExited;

                _apiProcess.Start();
                ShowNotification("Started", ToolTipIcon.Info);

                // Schedule status check after startup delay
                Task.Delay(_startupDelay).ContinueWith(_ =>
                {
                    _ = CheckStatusAsync();
                }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to start service:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                CleanupProcess();
            }
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        lock (_stateLock)
        {
            _isActuallyRunning = false;
        }

        _syncContext.Post(_ =>
        {
            if (!_isDisposed)
            {
                UpdateUI(false);
                ShowNotification("Service stopped unexpectedly", ToolTipIcon.Warning);
            }
        }, null);
    }

    private void StopService()
    {
        if (_isDisposed) return;

        lock (_processLock)
        {
            try
            {
                // Stop our tracked process
                if (_apiProcess != null && !_apiProcess.HasExited)
                {
                    _apiProcess.Kill(true);

                    if (!_apiProcess.WaitForExit(3000))
                    {
                        Debug.WriteLine("Process did not exit gracefully");
                    }
                }

                // Clean up any orphaned processes
                KillOrphanedProcesses();

                CleanupProcess();

                // Update running state
                lock (_stateLock)
                {
                    _isActuallyRunning = false;
                }

                UpdateUI(false);
                ShowNotification("Stopped", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to stop service:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }

    private void KillOrphanedProcesses()
    {
        try
        {
            var processes = Process.GetProcessesByName(_processName);
            foreach (var process in processes)
            {
                try
                {
                    using (process)
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                            process.WaitForExit(1000);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to kill orphaned process {process.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enumerate processes: {ex.Message}");
        }
    }

    private void CleanupProcess()
    {
        if (_apiProcess != null)
        {
            _apiProcess.Exited -= OnProcessExited;
            _apiProcess.Dispose();
            _apiProcess = null;
        }
    }

    private bool IsProcessRunning()
    {
        lock (_processLock)
        {
            return _apiProcess != null && !_apiProcess.HasExited;
        }
    }

    private async Task<bool> IsApiRespondingAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(_apiHealthUrl);
            return response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException)
        {
            // Timeout
            return false;
        }
        catch (HttpRequestException)
        {
            // Connection failed
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Health check failed: {ex.Message}");
            return false;
        }
    }

    private Icon CreateIcon(bool running)
    {
        var bmp = new Bitmap(16, 16);

        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (var pen = new Pen(Color.Black, 1))
            using (var statusBrush = new SolidBrush(running ? Color.LimeGreen : Color.Red))
            {
                // Draw document outline
                g.DrawRectangle(pen, 2, 2, 12, 12);

                // Draw status indicator
                if (running)
                {
                    // Running = scanner line
                    g.FillRectangle(statusBrush, 3, 7, 10, 2);
                }
                else
                {
                    // Stopped = block
                    g.FillRectangle(statusBrush, 5, 5, 6, 6);
                }
            }
        }

        return Icon.FromHandle(bmp.GetHicon());
    }

    private void ShowNotification(string message, ToolTipIcon icon)
    {
        try
        {
            if (!_isDisposed && _icon != null)
            {
                _icon.ShowBalloonTip(2000, "Scanner", message, icon);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show notification: {ex.Message}");
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Exit Scanner Service?\n\nScanning will be unavailable.",
            "Exit",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            StopService();

            Dispose();
            Application.Exit();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
        {
            _isDisposed = true;

            // Stop timer
            if (_statusCheckTimer != null)
            {
                _statusCheckTimer.Stop();
                _statusCheckTimer.Tick -= OnStatusCheckTimerTick;
                _statusCheckTimer.Dispose();
            }

            // Stop service
            StopService();

            // Dispose HTTP client
            _httpClient?.Dispose();

            // Hide and dispose icon
            if (_icon != null)
            {
                _icon.Visible = false;
                var currentIcon = _icon.Icon;
                _icon.Icon = null;
                currentIcon?.Dispose();
                _icon.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}