using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json;

namespace YimMenuV2_Launchpad
{
    // Classes to deserialize GitHub API response
    public class GitHubRelease
    {
        [JsonProperty("assets")]
        public List<GitHubAsset> Assets { get; set; } = new List<GitHubAsset>();
    }

    public class GitHubAsset
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("digest")]
        public string Digest { get; set; } = string.Empty;

        [JsonProperty("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize
        );

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // DLL Injection APIs
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
            uint processAccess,
            bool bInheritHandle,
            int processId
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            uint dwSize,
            uint flAllocationType,
            uint flProtect
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            uint nSize,
            out UIntPtr lpNumberOfBytesWritten
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(
            IntPtr hProcess,
            IntPtr lpThreadAttributes,
            uint dwStackSize,
            IntPtr lpStartAddress,
            IntPtr lpParameter,
            uint dwCreationFlags,
            IntPtr lpThreadId
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            uint dwSize,
            uint dwFreeType
        );

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int GWL_STYLE = -16;
        private const int WS_MINIMIZEBOX = 0x20000;

        // DLL Injection constants
        private const uint PROCESS_CREATE_THREAD = 0x0002;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint MEM_COMMIT = 0x00001000;
        private const uint MEM_RESERVE = 0x00002000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 4;
        private const uint INFINITE = 0xFFFFFFFF;

        // Variables for process monitoring
        private DispatcherTimer? processMonitorTimer;
        private bool isGameRunning = false;
        private const string TARGET_PROCESS_NAME = "GTA5_Enhanced";

        // Configuration variables
        private string configFilePath = string.Empty;
        private const string CONFIG_FILE_NAME = "launchpad_config.txt";
        private const string HASH_FILE_NAME = "hash.txt";
        private const string GITHUB_API_URL =
            "https://api.github.com/repos/YimMenu/YimMenuV2/releases/tags/nightly";

        // HttpClient for HTTP requests
        private static readonly HttpClient httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "YimMenuV2-Launchpad/1.0");
            return client;
        }

        public MainWindow()
        {
            InitializeComponent();
            InitializeFolders();
            LoadConfiguration();
            InitializeProcessMonitor();

            // Connect the event to save configuration when selection changes
            PlatformComboBox.SelectionChanged += PlatformComboBox_SelectionChanged;

            // Check for updates on startup
            _ = CheckForUpdatesAsync();
        }

        private bool InjectDLL(string processName, string dllPath)
        {
            try
            {
                // Verify that the DLL exists
                if (!File.Exists(dllPath))
                {
                    UpdateStatus($"Error: DLL not found at {dllPath}");
                    return false;
                }

                // Find the process
                Process[] processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                {
                    UpdateStatus(
                        $"Error: Process '{processName}' not found. Make sure the game is running."
                    );
                    return false;
                }

                Process targetProcess = processes[0];
                UpdateStatus($"Found process {processName} (PID: {targetProcess.Id})");

                // Open the process with necessary permissions
                IntPtr processHandle = OpenProcess(
                    PROCESS_CREATE_THREAD
                        | PROCESS_QUERY_INFORMATION
                        | PROCESS_VM_OPERATION
                        | PROCESS_VM_WRITE
                        | PROCESS_VM_READ,
                    false,
                    targetProcess.Id
                );

                if (processHandle == IntPtr.Zero)
                {
                    UpdateStatus(
                        "Error: Could not open target process. Try running as administrator."
                    );
                    return false;
                }

                try
                {
                    // Get LoadLibraryA address
                    IntPtr kernel32Handle = GetModuleHandle("kernel32.dll");
                    IntPtr loadLibraryAddr = GetProcAddress(kernel32Handle, "LoadLibraryA");

                    if (loadLibraryAddr == IntPtr.Zero)
                    {
                        UpdateStatus("Error: Could not find LoadLibraryA address");
                        return false;
                    }

                    // Convert DLL path to bytes
                    byte[] dllBytes = Encoding.ASCII.GetBytes(dllPath + "\0");

                    // Reserve memory in target process
                    IntPtr allocMemAddress = VirtualAllocEx(
                        processHandle,
                        IntPtr.Zero,
                        (uint)dllBytes.Length,
                        MEM_COMMIT | MEM_RESERVE,
                        PAGE_READWRITE
                    );

                    if (allocMemAddress == IntPtr.Zero)
                    {
                        UpdateStatus("Error: Could not allocate memory in target process");
                        return false;
                    }

                    try
                    {
                        // Write DLL path to process memory
                        if (
                            !WriteProcessMemory(
                                processHandle,
                                allocMemAddress,
                                dllBytes,
                                (uint)dllBytes.Length,
                                out UIntPtr bytesWritten
                            )
                        )
                        {
                            UpdateStatus(
                                "Error: Could not write DLL path to target process memory"
                            );
                            return false;
                        }

                        // Create a remote thread that executes LoadLibraryA
                        IntPtr threadHandle = CreateRemoteThread(
                            processHandle,
                            IntPtr.Zero,
                            0,
                            loadLibraryAddr,
                            allocMemAddress,
                            0,
                            IntPtr.Zero
                        );

                        if (threadHandle == IntPtr.Zero)
                        {
                            UpdateStatus("Error: Could not create remote thread");
                            return false;
                        }

                        try
                        {
                            // Wait for the thread to finish
                            WaitForSingleObject(threadHandle, INFINITE);
                            UpdateStatus("DLL injection completed successfully!");
                            return true;
                        }
                        finally
                        {
                            CloseHandle(threadHandle);
                        }
                    }
                    finally
                    {
                        // Free the reserved memory
                        VirtualFreeEx(processHandle, allocMemAddress, 0, MEM_RELEASE);
                    }
                }
                finally
                {
                    CloseHandle(processHandle);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error during DLL injection: {ex.Message}");
                return false;
            }
        }

        private void InitializeProcessMonitor()
        {
            // Create timer to check process every 2 seconds
            processMonitorTimer = new DispatcherTimer();
            processMonitorTimer.Interval = TimeSpan.FromSeconds(2);
            processMonitorTimer.Tick += ProcessMonitorTimer_Tick;
            processMonitorTimer.Start();

            // Check immediately on startup
            CheckGameProcess();
        }

        private void ProcessMonitorTimer_Tick(object? sender, EventArgs e)
        {
            CheckGameProcess();
        }

        private void CheckGameProcess()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(TARGET_PROCESS_NAME);
                bool gameCurrentlyRunning = processes.Length > 0;

                // Only update if state changed
                if (gameCurrentlyRunning != isGameRunning)
                {
                    isGameRunning = gameCurrentlyRunning;
                    UpdateButtonStyles();
                }
            }
            catch (Exception ex)
            {
                // Silent error to avoid spamming status
                System.Diagnostics.Debug.WriteLine($"Error checking process: {ex.Message}");
            }
        }

        private void UpdateButtonStyles()
        {
            try
            {
                if (isGameRunning)
                {
                    // Game is running: Launch dark, Inject blue
                    SetButtonStyle(LaunchButton, false); // false = dark style
                    SetButtonStyle(InjectButton, true); // true = blue style
                }
                else
                {
                    // Game NOT running: Launch blue, Inject dark
                    SetButtonStyle(LaunchButton, true); // true = blue style
                    SetButtonStyle(InjectButton, false); // false = dark style
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating button styles: {ex.Message}");
            }
        }

        private void SetButtonStyle(Button button, bool isPrimary)
        {
            try
            {
                if (isPrimary)
                {
                    // Blue style (Primary)
                    button.Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#0078D4")
                    );
                    button.BorderBrush = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#106EBE")
                    );
                }
                else
                {
                    // Dark style (Modern)
                    button.Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#2D2D30")
                    );
                    button.BorderBrush = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#3F3F46")
                    );
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting button style: {ex.Message}");
            }
        }

        private void LaunchSteamGame()
        {
            try
            {
                UpdateStatus("Attempting to launch GTA V via Steam...");

                // Method 1: Try launching using steam:// protocol
                try
                {
                    var steamProcess = new ProcessStartInfo
                    {
                        FileName = "steam://run/3240220", // Correct App ID for GTA V
                        UseShellExecute = true,
                    };
                    Process.Start(steamProcess);
                    UpdateStatus("✅ Steam launch command sent successfully!");
                    return;
                }
                catch (Exception ex)
                {
                    UpdateStatus(
                        $"Steam protocol failed: {ex.Message}. Trying alternative method..."
                    );
                }

                // Method 2: Try launching Steam directly with parameters
                try
                {
                    // Look for Steam in common locations
                    string[] steamPaths =
                    {
                        @"C:\Program Files (x86)\Steam\Steam.exe",
                        @"C:\Program Files\Steam\Steam.exe",
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                            + @"\Steam\Steam.exe",
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                            + @"\Steam\Steam.exe",
                    };

                    string? steamPath = steamPaths.FirstOrDefault(File.Exists);

                    if (!string.IsNullOrEmpty(steamPath))
                    {
                        var steamProcess = new ProcessStartInfo
                        {
                            FileName = steamPath,
                            Arguments = "-applaunch 3240220", // GTA V App ID
                            UseShellExecute = false,
                        };
                        Process.Start(steamProcess);
                        UpdateStatus("✅ GTA V launched via Steam executable!");
                        return;
                    }
                    else
                    {
                        UpdateStatus("❌ Steam installation not found in common locations.");
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"❌ Failed to launch via Steam executable: {ex.Message}");
                }

                // Method 3: Open Steam and show message
                try
                {
                    var steamProcess = new ProcessStartInfo
                    {
                        FileName = "steam://open/main",
                        UseShellExecute = true,
                    };
                    Process.Start(steamProcess);
                    UpdateStatus("Steam opened. Please manually launch GTA V from your library.");
                }
                catch (Exception ex)
                {
                    UpdateStatus(
                        $"❌ All launch methods failed. Please open Steam manually and launch GTA V. Error: {ex.Message}"
                    );
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ Critical error launching Steam: {ex.Message}");
            }
        }

        private void InitializeFolders()
        {
            try
            {
                // Define the YimMenuV2 folder path
                string yimMenuPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YimMenuV2"
                );

                // If the YimMenuV2 folder doesn't exist, create it
                if (!Directory.Exists(yimMenuPath))
                {
                    Directory.CreateDirectory(yimMenuPath);
                }

                // Define the launchpad folder path inside YimMenuV2
                string launchpadPath = System.IO.Path.Combine(yimMenuPath, "launchpad");

                // If the launchpad folder doesn't exist, create it
                if (!Directory.Exists(launchpadPath))
                {
                    Directory.CreateDirectory(launchpadPath);
                }

                // Set the configuration file path
                configFilePath = System.IO.Path.Combine(launchpadPath, CONFIG_FILE_NAME);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error initializing folders: {ex.Message}");
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string[] configLines = File.ReadAllLines(configFilePath);

                    foreach (string line in configLines)
                    {
                        if (line.StartsWith("LastSelectedPlatform="))
                        {
                            string platformName = line.Substring("LastSelectedPlatform=".Length);
                            SetSelectedPlatform(platformName);
                            break;
                        }
                    }

                    // UpdateStatus("Configuration loaded successfully.");
                }
                else
                {
                    // If the file doesn't exist, use default configuration (Epic Games)
                    PlatformComboBox.SelectedIndex = 0;
                    // UpdateStatus("Using default configuration (Epic Games).");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading configuration: {ex.Message}");
                // Use default configuration in case of error
                PlatformComboBox.SelectedIndex = 0;
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                if (
                    PlatformComboBox.SelectedItem is ComboBoxItem selectedItem
                    && selectedItem.Content is string platformName
                )
                {
                    string configContent = $"LastSelectedPlatform={platformName}";
                    File.WriteAllText(configFilePath, configContent);
                    // UpdateStatus($"Configuration saved - Platform: {platformName}");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error saving configuration: {ex.Message}");
            }
        }

        private void SetSelectedPlatform(string platformName)
        {
            try
            {
                for (int i = 0; i < PlatformComboBox.Items.Count; i++)
                {
                    if (
                        PlatformComboBox.Items[i] is ComboBoxItem item
                        && item.Content is string content
                        && content == platformName
                    )
                    {
                        PlatformComboBox.SelectedIndex = i;
                        return;
                    }
                }

                // If platform is not found, use the first one as default
                PlatformComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error setting platform: {ex.Message}");
                PlatformComboBox.SelectedIndex = 0;
            }
        }

        private void PlatformComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only save if ComboBox is fully initialized
            if (PlatformComboBox.IsLoaded && configFilePath != null)
            {
                SaveConfiguration();
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;

            // Enable dark title bar
            int value = 1;
            if (
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int))
                != 0
            )
            {
                DwmSetWindowAttribute(
                    hwnd,
                    DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1,
                    ref value,
                    sizeof(int)
                );
            }

            // Remove minimize button
            int style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_MINIMIZEBOX);
        }

        protected override void OnClosed(EventArgs e)
        {
            // Save configuration before closing
            SaveConfiguration();

            // Clean up timer when closing window
            processMonitorTimer?.Stop();
            processMonitorTimer = null;

            // Clean up HttpClient
            httpClient?.Dispose();

            base.OnClosed(e);
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (
                    PlatformComboBox.SelectedItem is ComboBoxItem selectedItem
                    && selectedItem.Content is string content
                )
                {
                    string selectedPlatform = content;
                    UpdateStatus($"Launching game via {selectedPlatform}...");

                    // Here you can add specific logic for each platform
                    switch (selectedPlatform)
                    {
                        case "Epic Games":
                            // Logic for Epic Games
                            UpdateStatus("Epic Games launcher not implemented yet.");
                            break;
                        case "Steam":
                            LaunchSteamGame();
                            break;
                        case "Rockstar Games":
                            // Logic for Rockstar Games
                            UpdateStatus("Rockstar Games launcher not implemented yet.");
                            break;
                    }
                }
                else
                {
                    UpdateStatus("Please select a platform first.");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error launching game: {ex.Message}");
            }
        }

        private void InjectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Starting YimMenuV2 injection...");

                // Build DLL path
                string yimMenuPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YimMenuV2",
                    "launchpad"
                );

                string dllPath = System.IO.Path.Combine(yimMenuPath, "YimMenuV2.dll");

                // Verify that the DLL exists
                if (!File.Exists(dllPath))
                {
                    UpdateStatus(
                        $"❌ Error: YimMenuV2.dll not found in {yimMenuPath}. Please download and place the DLL in the launchpad folder."
                    );
                    return;
                }

                // Try to inject DLL into GTA5_Enhanced.exe process
                string processName = TARGET_PROCESS_NAME;
                UpdateStatus($"Looking for process: {processName}.exe...");

                bool injectionSuccess = InjectDLL(processName, dllPath);

                if (injectionSuccess)
                {
                    UpdateStatus("✅ YimMenuV2 injected successfully into GTA5_Enhanced.exe!");
                }
                else
                {
                    UpdateStatus(
                        "❌ Failed to inject YimMenuV2. Common issues: Game not running, insufficient permissions, or antivirus blocking."
                    );
                }
            }
            catch (Exception ex)
            {
                UpdateStatus(
                    $"❌ Critical error during injection: {ex.Message}. Try running as administrator."
                );
            }
        }

        private async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                UpdateStatus("Checking for updates...");

                // Get the latest release information
                var latestRelease = await GetLatestReleaseAsync();
                if (latestRelease?.Assets == null)
                {
                    // UpdateStatus("Failed to check for updates from GitHub API");
                    return false;
                }

                // Look for the YimMenuV2.dll asset
                var dllAsset = latestRelease.Assets.FirstOrDefault(a => a.Name == "YimMenuV2.dll");
                if (dllAsset == null)
                {
                    UpdateStatus("YimMenuV2.dll not found in latest release");
                    return false;
                }

                // Extract SHA256 hash from digest field
                string latestHash = ExtractSha256FromDigest(dllAsset.Digest);
                if (string.IsNullOrEmpty(latestHash))
                {
                    UpdateStatus("Invalid hash format in release data");
                    return false;
                }

                // Read local hash
                string localHash = GetLocalHash();

                // Compare hashes
                if (localHash.Equals(latestHash, StringComparison.OrdinalIgnoreCase))
                {
                    UpdateStatus("YimMenuV2 is up to date!");
                    return false; // No updates available
                }

                // An update is available
                UpdateStatus("New version available! Downloading...");
                bool downloadSuccess = await DownloadLatestDllAsync(
                    dllAsset.BrowserDownloadUrl,
                    latestHash
                );

                if (downloadSuccess)
                {
                    UpdateStatus("✅ YimMenuV2 updated successfully!");
                    return true;
                }
                else
                {
                    UpdateStatus("❌ Failed to download update");
                    return false;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error checking for updates: {ex.Message}");
                return false;
            }
        }

        private async Task<GitHubRelease?> GetLatestReleaseAsync()
        {
            try
            {
                var response = await httpClient.GetStringAsync(GITHUB_API_URL);
                return JsonConvert.DeserializeObject<GitHubRelease>(response);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching release data: {ex.Message}");
                UpdateStatus($"Error fetching release data: {ex.Message}");
                return null;
            }
        }

        private string ExtractSha256FromDigest(string digest)
        {
            // The digest field has format "sha256:hash"
            if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                return digest.Substring(7); // Remove "sha256:" from the beginning
            }
            return string.Empty;
        }

        private string GetLocalHash()
        {
            try
            {
                string yimMenuPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YimMenuV2",
                    "launchpad"
                );

                string hashFilePath = System.IO.Path.Combine(yimMenuPath, HASH_FILE_NAME);

                if (File.Exists(hashFilePath))
                {
                    return File.ReadAllText(hashFilePath).Trim();
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading local hash: {ex.Message}");
                return string.Empty;
            }
        }

        private void SaveLocalHash(string hash)
        {
            try
            {
                string yimMenuPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YimMenuV2",
                    "launchpad"
                );

                string hashFilePath = System.IO.Path.Combine(yimMenuPath, HASH_FILE_NAME);
                File.WriteAllText(hashFilePath, hash);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving local hash: {ex.Message}");
            }
        }

        private async Task<bool> DownloadLatestDllAsync(string downloadUrl, string expectedHash)
        {
            try
            {
                string yimMenuPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YimMenuV2",
                    "launchpad"
                );

                string dllPath = System.IO.Path.Combine(yimMenuPath, "YimMenuV2.dll");
                string tempDllPath = System.IO.Path.Combine(yimMenuPath, "YimMenuV2.dll.tmp");

                // Download the file to a temporary file
                using (var response = await httpClient.GetAsync(downloadUrl))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        UpdateStatus($"Failed to download: HTTP {response.StatusCode}");
                        return false;
                    }

                    await using (var fileStream = new FileStream(tempDllPath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }
                }

                // Verify the hash of downloaded file
                string downloadedHash = CalculateFileHash(tempDllPath);
                if (!downloadedHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(tempDllPath); // Delete corrupted file
                    UpdateStatus("Downloaded file hash verification failed");
                    return false;
                }

                // Replace existing file
                if (File.Exists(dllPath))
                {
                    File.Delete(dllPath);
                }
                File.Move(tempDllPath, dllPath);

                // Save the new hash
                SaveLocalHash(expectedHash);

                return true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error downloading DLL: {ex.Message}");
                return false;
            }
        }

        private string CalculateFileHash(string filePath)
        {
            try
            {
                using (var sha256 = SHA256.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        byte[] hashBytes = sha256.ComputeHash(stream);
                        return Convert.ToHexString(hashBytes).ToLowerInvariant();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculating file hash: {ex.Message}");
                return string.Empty;
            }
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable button during verification
                UpdateButton.IsEnabled = false;
                UpdateButton.Content = "🔄 CHECKING...";

                await CheckForUpdatesAsync();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error checking updates: {ex.Message}");
            }
            finally
            {
                // Re-enable button
                UpdateButton.IsEnabled = true;
                UpdateButton.Content = "🔄 UPDATE";
            }
        }

        private void ChangelogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Opening changelog...");

                // Open changelog in default browser
                string changelogUrl = "https://github.com/YimMenu/YimMenuV2/releases/latest";
                Process.Start(
                    new ProcessStartInfo { FileName = changelogUrl, UseShellExecute = true }
                );

                UpdateStatus("Changelog opened in browser");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error opening changelog: {ex.Message}");
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // UpdateStatus("Opening YimMenuV2 folder...");

                // Define the YimMenuV2 folder path
                // You can change this path according to where it's installed
                string yimMenuPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YimMenuV2"
                );

                // If the folder doesn't exist, create it
                if (!Directory.Exists(yimMenuPath))
                {
                    Directory.CreateDirectory(yimMenuPath);
                }

                // Open the folder in file explorer
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = yimMenuPath,
                        UseShellExecute = true,
                        Verb = "open",
                    }
                );

                UpdateStatus("YimMenuV2 folder opened");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error opening folder: {ex.Message}");
            }
        }

        private void UpdateStatus(string message)
        {
            StatusTextBlock.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
        }
    }
}
