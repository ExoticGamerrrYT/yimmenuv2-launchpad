using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace YimMenuV2_Launchpad
{
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

        // Variables para el monitoreo del proceso
        private DispatcherTimer processMonitorTimer;
        private bool isGameRunning = false;
        private const string TARGET_PROCESS_NAME = "GTA5_Enhanced";

        // Variables para configuración
        private string configFilePath;
        private const string CONFIG_FILE_NAME = "launchpad_config.txt";

        public MainWindow()
        {
            InitializeComponent();
            InitializeFolders();
            LoadConfiguration();
            InitializeProcessMonitor();

            // Conectar el evento para guardar la configuración cuando cambie la selección
            PlatformComboBox.SelectionChanged += PlatformComboBox_SelectionChanged;
        }

        private bool InjectDLL(string processName, string dllPath)
        {
            try
            {
                // Verificar que el DLL existe
                if (!File.Exists(dllPath))
                {
                    UpdateStatus($"Error: DLL not found at {dllPath}");
                    return false;
                }

                // Buscar el proceso
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

                // Abrir el proceso con los permisos necesarios
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
                    // Obtener la dirección de LoadLibraryA
                    IntPtr kernel32Handle = GetModuleHandle("kernel32.dll");
                    IntPtr loadLibraryAddr = GetProcAddress(kernel32Handle, "LoadLibraryA");

                    if (loadLibraryAddr == IntPtr.Zero)
                    {
                        UpdateStatus("Error: Could not find LoadLibraryA address");
                        return false;
                    }

                    // Convertir la ruta del DLL a bytes
                    byte[] dllBytes = Encoding.ASCII.GetBytes(dllPath + "\0");

                    // Reservar memoria en el proceso objetivo
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
                        // Escribir la ruta del DLL en la memoria del proceso
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

                        // Crear un hilo remoto que ejecute LoadLibraryA
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
                            // Esperar a que el hilo termine
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
                        // Liberar la memoria reservada
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
            // Crear timer para verificar el proceso cada 2 segundos
            processMonitorTimer = new DispatcherTimer();
            processMonitorTimer.Interval = TimeSpan.FromSeconds(2);
            processMonitorTimer.Tick += ProcessMonitorTimer_Tick;
            processMonitorTimer.Start();

            // Verificar inmediatamente al iniciar
            CheckGameProcess();
        }

        private void ProcessMonitorTimer_Tick(object sender, EventArgs e)
        {
            CheckGameProcess();
        }

        private void CheckGameProcess()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(TARGET_PROCESS_NAME);
                bool gameCurrentlyRunning = processes.Length > 0;

                // Solo actualizar si el estado cambió
                if (gameCurrentlyRunning != isGameRunning)
                {
                    isGameRunning = gameCurrentlyRunning;
                    UpdateButtonStyles();
                }
            }
            catch (Exception ex)
            {
                // Error silencioso para no spam en el status
                System.Diagnostics.Debug.WriteLine($"Error checking process: {ex.Message}");
            }
        }

        private void UpdateButtonStyles()
        {
            try
            {
                if (isGameRunning)
                {
                    // Game está corriendo: Launch oscuro, Inject azul
                    SetButtonStyle(LaunchButton, false); // false = estilo oscuro
                    SetButtonStyle(InjectButton, true); // true = estilo azul
                }
                else
                {
                    // Game NO está corriendo: Launch azul, Inject oscuro
                    SetButtonStyle(LaunchButton, true); // true = estilo azul
                    SetButtonStyle(InjectButton, false); // false = estilo oscuro
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
                    // Estilo azul (Primary)
                    button.Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#0078D4")
                    );
                    button.BorderBrush = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#106EBE")
                    );
                }
                else
                {
                    // Estilo oscuro (Modern)
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

                // Método 1: Intentar lanzar usando steam:// protocol
                try
                {
                    var steamProcess = new ProcessStartInfo
                    {
                        FileName = "steam://run/3240220", // App ID correcto para GTA V
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

                // Método 2: Intentar lanzar Steam directamente con parámetros
                try
                {
                    // Buscar Steam en ubicaciones comunes
                    string[] steamPaths =
                    {
                        @"C:\Program Files (x86)\Steam\Steam.exe",
                        @"C:\Program Files\Steam\Steam.exe",
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                            + @"\Steam\Steam.exe",
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                            + @"\Steam\Steam.exe",
                    };

                    string steamPath = steamPaths.FirstOrDefault(File.Exists);

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

                // Método 3: Abrir Steam y mostrar mensaje
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
                // Define la ruta de la carpeta de YimMenuV2
                string yimMenuPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YimMenuV2"
                );

                // Si la carpeta YimMenuV2 no existe, créala
                if (!Directory.Exists(yimMenuPath))
                {
                    Directory.CreateDirectory(yimMenuPath);
                }

                // Define la ruta de la carpeta launchpad dentro de YimMenuV2
                string launchpadPath = System.IO.Path.Combine(yimMenuPath, "launchpad");

                // Si la carpeta launchpad no existe, créala
                if (!Directory.Exists(launchpadPath))
                {
                    Directory.CreateDirectory(launchpadPath);
                }

                // Establecer la ruta del archivo de configuración
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
                    // Si no existe el archivo, usar la configuración por defecto (Epic Games)
                    PlatformComboBox.SelectedIndex = 0;
                    // UpdateStatus("Using default configuration (Epic Games).");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading configuration: {ex.Message}");
                // Usar configuración por defecto en caso de error
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

                // Si no se encuentra la plataforma, usar la primera por defecto
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
            // Solo guardar si el ComboBox ya está completamente inicializado
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
            // Guardar configuración antes de cerrar
            SaveConfiguration();

            // Limpiar el timer al cerrar la ventana
            processMonitorTimer?.Stop();
            processMonitorTimer = null;
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

                    // Aquí puedes agregar la lógica específica para cada plataforma
                    switch (selectedPlatform)
                    {
                        case "Epic Games":
                            // Lógica para Epic Games
                            UpdateStatus("Epic Games launcher not implemented yet.");
                            break;
                        case "Steam":
                            LaunchSteamGame();
                            break;
                        case "Rockstar Games":
                            // Lógica para Rockstar Games
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

                // Construir la ruta del DLL
                string yimMenuPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YimMenuV2",
                    "launchpad"
                );

                string dllPath = System.IO.Path.Combine(yimMenuPath, "YimMenuV2.dll");

                // Verificar que el DLL existe
                if (!File.Exists(dllPath))
                {
                    UpdateStatus(
                        $"❌ Error: YimMenuV2.dll not found in {yimMenuPath}. Please download and place the DLL in the launchpad folder."
                    );
                    return;
                }

                // Intentar inyectar el DLL en el proceso GTA5_Enhanced.exe
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

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Checking for updates...");

                // Aquí agregas la lógica para verificar y descargar actualizaciones
                // Puedes usar GitHub API o cualquier otro servicio

                UpdateStatus("YimMenuV2 is up to date!");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error checking updates: {ex.Message}");
            }
        }

        private void ChangelogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Opening changelog...");

                // Abre el changelog en el navegador predeterminado
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

                // Define la ruta de la carpeta de YimMenuV2
                // Puedes cambiar esta ruta según donde esté instalado
                string yimMenuPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YimMenuV2"
                );

                // Si la carpeta no existe, créala
                if (!Directory.Exists(yimMenuPath))
                {
                    Directory.CreateDirectory(yimMenuPath);
                }

                // Abre la carpeta en el explorador de archivos
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
