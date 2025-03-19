using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using HtmlAgilityPack;
using System.Text.Json;
using System.Windows.Forms;
using HtmlDocument = HtmlAgilityPack.HtmlDocument; // Resolver la referencia ambigua
using PathIO = System.IO.Path; // Resolver la referencia ambigua
using ColorMedia = System.Windows.Media.Color; // Resolve ambiguous reference
using ColorConverterMedia = System.Windows.Media.ColorConverter; // Resolve ambiguous reference
using WpfButton = System.Windows.Controls.Button; // Resolve ambiguous reference
using System.Media; // Agregar la directiva using para SoundPlayer

namespace SyncTranslator_SC
{
    public partial class MainWindow : Window
    {
        private const string CacheFilePath = "cache.json";
        private const string LivePath = @"C:\Program Files\Roberts Space Industries\StarCitizen\LIVE";
        private const string PtuPath = @"C:\Program Files\Roberts Space Industries\StarCitizen\PTU";
        private const string TempDirectory = @"C:\Temp\SyncTranslatorSC";
        private static readonly string LogFilePath = PathIO.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "log.txt");
        private static System.Timers.Timer updateTimer = new System.Timers.Timer();
        private System.Windows.Forms.NotifyIcon notifyIcon = new System.Windows.Forms.NotifyIcon(); // Initialize to resolve non-nullable field warning
        private static readonly HttpClient client = new HttpClient();
        private int totalProcesses = 0;
        private int completedProcesses = 0;

        public MainWindow()
        {
            InitializeComponent();
            Log("Aplicación iniciada.");
            CheckDirectories();
            StartUpdateTimer();
            InitializeNotifyIcon();
            CheckGameRunningAndUpdate();
            Log("Inicialización completada.");

            // Cargar el directorio seleccionado manualmente
            var selectedDirectory = LoadSelectedDirectory();
            if (!string.IsNullOrEmpty(selectedDirectory))
            {
                Log($"Directorio seleccionado manualmente cargado: {selectedDirectory}");
            }
        }

        private async void VerifyUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            Log("Botón 'Verificar Actualización' presionado.");
            var processes = Process.GetProcessesByName("StarCitizen");
            if (processes.Length > 0)
            {
                ShowNotification("Star Citizen ejecutándose, por favor cierre el juego para actualizar la traducción.");
            }
            else
            {
                await DownloadAndUpdateFiles();
            }
        }

        private void MinimizeToTrayButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized; // Minimiza la ventana
            this.Hide(); // Oculta la ventana
            notifyIcon.Visible = true; // Muestra el icono en la bandeja del sistema
            ShowNotification("La aplicación está minimizada en la barra de notificaciones.");
            Log("Aplicación minimizada a la bandeja del sistema.");
        }

        private void ClosePopupButton_Click(object sender, RoutedEventArgs e)
        {
            MinimizePopup.IsOpen = false;
        }

        // ...existing code...
        private void ShowNotification(string message)
        {
            Dispatcher.Invoke(() =>
            {
                // Reproducir el sonido de notificación
                try
                {
                    var player = new SoundPlayer("notificacion.wav");
                    player.Play();
                }
                catch (Exception ex)
                {
                    // Manejar cualquier error que ocurra al reproducir el sonido
                    System.Windows.MessageBox.Show($"Error al reproducir el sonido de notificación: {ex.Message}");
                }

                // Asegurarse de que el notifyIcon esté visible
                if (!notifyIcon.Visible)
                {
                    notifyIcon.Visible = true;
                }

                notifyIcon.BalloonTipTitle = "SyncTranslator SC";
                notifyIcon.BalloonTipText = message;
                notifyIcon.ShowBalloonTip(3000);
            });
        }

        private void Log(string message)
        {
            Directory.CreateDirectory(PathIO.GetDirectoryName(LogFilePath) ?? string.Empty);
            File.AppendAllText(LogFilePath, $"{DateTime.Now}: {message}\n");
            Dispatcher.Invoke(() => LogListBox.Items.Add(message));
        }

        private void CheckDirectories()
        {
            Log("Verificando directorios...");
            CheckDirectory(LivePath, LiveLed);
            CheckDirectory(PtuPath, PtuLed);
            Log("Verificación de directorios completada.");
        }

        private async Task StartUpdateTimer()
        {
            Log("Iniciando temporizador de actualización...");
            updateTimer.Interval = 3600000; // 1 hora
            updateTimer.Elapsed += async (sender, e) => await CheckForNewVersionAsync();
            updateTimer.Start();
            Log("Temporizador de actualización iniciado.");
        }

        private async Task CheckForNewVersionAsync()
        {
            Log("Verificando si hay una nueva versión...");
            string? latestZipUrl = await GetLatestZipUrlFromHtmlAsync();
            if (latestZipUrl == null)
            {
                Log("No se pudo encontrar un enlace al archivo ZIP.");
                return;
            }

            DateTime latestZipDate = await GetZipDateAsync(latestZipUrl);
            DateTime? cachedZipDate = GetCachedZipDate();

            if (cachedZipDate == null || latestZipDate > cachedZipDate)
            {
                ShowNotification("EXISTE UNA NUEVA TRADUCCIÓN Actualicé a la versión más reciente");
                await DownloadAndUpdateFiles();
                CacheZipDate(latestZipDate);
            }
            else
            {
                Log("No hay una nueva versión disponible.");
            }
        }

        private async Task<DateTime> GetZipDateAsync(string zipUrl)
        {
            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, zipUrl));
            response.EnsureSuccessStatusCode();
            return response.Content.Headers.LastModified?.UtcDateTime ?? DateTime.UtcNow;
        }

        private DateTime? GetCachedZipDate()
        {
            if (!File.Exists(CacheFilePath))
            {
                return null;
            }

            string json = File.ReadAllText(CacheFilePath);
            var cache = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json);
            return cache?.GetValueOrDefault("latestZipDate");
        }

        private void CacheZipDate(DateTime date)
        {
            var cache = new Dictionary<string, DateTime>
            {
                { "latestZipDate", date }
            };

            string json = JsonSerializer.Serialize(cache);
            File.WriteAllText(CacheFilePath, json);
        }

        private async Task<string?> GetLatestZipUrlFromHtmlAsync()
        {
            try
            {
                Log("Accediendo a la página de releases de GitHub...");
                string releasesUrl = "https://github.com/Thord82/Star_citizen_ES/releases";

                var response = await client.GetAsync(releasesUrl);
                response.EnsureSuccessStatusCode();
                string htmlContent = await response.Content.ReadAsStringAsync();

                Log("HTML descargado con éxito.");

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlContent);

                var zipNode = htmlDoc.DocumentNode.SelectSingleNode("//a[contains(@href, 'Star_citizen_ES.zip')]");
                if (zipNode == null)
                {
                    Log("Error: No se encontró un enlace para 'Star_citizen_ES.zip'.");
                    return null;
                }

                string zipUrl = zipNode.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(zipUrl))
                {
                    Log("Error: El enlace al archivo 'Star_citizen_ES.zip' no contiene una URL válida.");
                    return null;
                }

                if (!zipUrl.StartsWith("http"))
                {
                    zipUrl = "https://github.com" + zipUrl;
                }

                Log($"Enlace al archivo ZIP encontrado: {zipUrl}");
                return zipUrl;
            }
            catch (Exception ex)
            {
                Log($"Error al analizar la página de releases: {ex.Message}");
                return null;
            }
        }

        private async Task DownloadAndUpdateFiles()
        {
            Log("Iniciando descarga y actualización de archivos...");
            totalProcesses = 3;
            completedProcesses = 0;
            UpdateProgressBar();

            if (await DownloadAndExtractZipFromHtmlAsync())
            {
                CleanUp();
                completedProcesses++;
                UpdateProgressBar();
            }
            Log("Descarga y actualización de archivos completada.");
        }

        private async Task<bool> DownloadAndExtractZipFromHtmlAsync()
        {
            string? zipUrl = await GetLatestZipUrlFromHtmlAsync();
            if (zipUrl == null)
            {
                Log("No se pudo encontrar un enlace al archivo ZIP.");
                return false;
            }

            try
            {
                string zipPath = PathIO.Combine(TempDirectory, "Star_Citizen_ES.zip");
                string extractPath = PathIO.Combine(TempDirectory, "Extracted");

                Log("Descargando el archivo ZIP...");
                var response = await client.GetAsync(zipUrl);
                response.EnsureSuccessStatusCode();
                byte[] zipContent = await response.Content.ReadAsByteArrayAsync();
                Directory.CreateDirectory(TempDirectory);
                await File.WriteAllBytesAsync(zipPath, zipContent);

                Log("Archivo ZIP descargado exitosamente.");
                completedProcesses++;
                UpdateProgressBar();

                ExtractZipFile(zipPath, extractPath);
                CopyExtractedFiles(extractPath);

                completedProcesses++;
                UpdateProgressBar();

                return true;
            }
            catch (Exception ex)
            {
                Log($"Error al descargar o procesar el archivo ZIP: {ex.Message}");
                return false;
            }
        }

        private void ExtractZipFile(string zipPath, string extractPath)
        {
            try
            {
                Log("Extrayendo archivo ZIP...");

                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                    Log($"Directorio de extracción limpiado: {extractPath}");
                }

                Directory.CreateDirectory(extractPath);
                ZipFile.ExtractToDirectory(zipPath, extractPath);
                Log($"Archivo ZIP extraído exitosamente en: {extractPath}");
            }
            catch (Exception ex)
            {
                Log($"Error durante la extracción del archivo ZIP: {ex.Message}");
                throw;
            }
        }

        private void CopyExtractedFiles(string extractedPath)
        {
            try
            {
                // Obtener el directorio seleccionado manualmente
                var selectedDirectory = LoadSelectedDirectory();

                // Procesar la carpeta LIVE
                string liveSource = PathIO.Combine(extractedPath, "LIVE");
                if (Directory.Exists(liveSource))
                {
                    string liveTarget = !string.IsNullOrEmpty(selectedDirectory) ? PathIO.Combine(selectedDirectory, "LIVE") : LivePath;
                    Directory.CreateDirectory(liveTarget); // Crear la carpeta LIVE si no existe
                    CopyFiles(liveSource, liveTarget);
                }
                else
                {
                    Log("Error: No se encontró la carpeta LIVE en el ZIP extraído.");
                }

                // Procesar la carpeta PTU
                string ptuSource = PathIO.Combine(extractedPath, "PTU");
                if (Directory.Exists(ptuSource))
                {
                    string ptuTarget = !string.IsNullOrEmpty(selectedDirectory) ? PathIO.Combine(selectedDirectory, "PTU") : PtuPath;
                    Directory.CreateDirectory(ptuTarget); // Crear la carpeta PTU si no existe
                    CopyFiles(ptuSource, ptuTarget);
                }
                else
                {
                    Log("Error: No se encontró la carpeta PTU en el ZIP extraído.");
                }

                Log("Todos los archivos han sido copiados exitosamente.");
            }
            catch (Exception ex)
            {
                Log($"Error al procesar y copiar los archivos extraídos: {ex.Message}");
            }
        }

        private void CopyFiles(string sourceDir, string targetDir)
        {
            Log($"Copiando archivos de {sourceDir} a {targetDir}...");

            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = PathIO.Combine(targetDir, PathIO.GetFileName(file));

                if (File.Exists(destFile))
                {
                    File.Delete(destFile);
                    Log($"Archivo existente eliminado: {destFile}");
                }

                File.Copy(file, destFile, true);
                Log($"Archivo copiado: {file} -> {destFile}");
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destDir = PathIO.Combine(targetDir, PathIO.GetFileName(dir));
                CopyFiles(dir, destDir);
            }

            Log($"Copia de archivos completada en el destino: {targetDir}");
        }

        private void CleanUp()
        {
            Log("Limpiando archivos temporales...");
            try
            {
                string rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string tempExtractDirectory = PathIO.Combine(rootDirectory, "TempExtract");
                if (Directory.Exists(tempExtractDirectory))
                {
                    Directory.Delete(tempExtractDirectory, true);
                    Log("Archivos temporales eliminados exitosamente.");
                }
            }
            catch (Exception ex)
            {
                Log($"Error cleaning up: {ex.Message}");
            }
        }

        private void CheckDirectory(string path, Ellipse led)
        {
            Log($"Verificando directorio: {path}");
            if (Directory.Exists(path))
            {
                led.Fill = new SolidColorBrush((ColorMedia)ColorConverterMedia.ConvertFromString("#00FF7F"));
                StartFlashing(led);
                Log($"Directorio encontrado: {path}");
            }
            else
            {
                led.Fill = new SolidColorBrush((ColorMedia)ColorConverterMedia.ConvertFromString("#FF0606"));
                Log($"Directorio no encontrado: {path}");
            }
        }

        private void StartFlashing(Ellipse led)
        {
            var animation = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
            bool isOn = false;
            animation.Tick += (sender, args) =>
            {
                led.Fill = new SolidColorBrush(isOn ? (ColorMedia)ColorConverterMedia.ConvertFromString("#00FF7F") : (ColorMedia)ColorConverterMedia.ConvertFromString("#FF88F2F7"));
                isOn = !isOn;
            };
            animation.Start();
        }

        private void UpdateProgressBar()
        {
            Dispatcher.Invoke(() =>
            {
                OverallProgressBar.Value = (double)completedProcesses / totalProcesses * 100;
                if (OverallProgressBar.Value == 100)
                {
                    CompletionTextBlock.Visibility = Visibility.Visible;
                }
            });
        }

        // ...existing code...
        private void InitializeNotifyIcon()
        {
            Log("Inicializando icono de notificación...");
            try
            {
                notifyIcon = new System.Windows.Forms.NotifyIcon
                {
                    Icon = new System.Drawing.Icon("icono.ico"),
                    Visible = false // Inicialmente no visible
                };
            }
            catch (FileNotFoundException ex)
            {
                Log($"Error al inicializar el icono de notificación: {ex.Message}");
                System.Windows.MessageBox.Show($"Error al inicializar el icono de notificación: {ex.Message}");
                return;
            }

            notifyIcon.DoubleClick += (s, e) =>
            {
                this.Show(); // Muestra la ventana principal al hacer doble clic en el icono
                this.WindowState = WindowState.Normal; // Restaura la ventana a su estado normal
                notifyIcon.Visible = false; // Oculta el icono de la bandeja del sistema
            };

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("Cerrar", null, (s, e) => System.Windows.Application.Current.Shutdown());
            notifyIcon.ContextMenuStrip = contextMenu;
            Log("Icono de notificación inicializado.");
        }

        private async void CheckGameRunningAndUpdate()
        {
            var processes = Process.GetProcessesByName("StarCitizen");
            if (processes.Length > 0)
            {
                ShowNotification("Star Citizen ejecutándose, por favor cierre el juego para actualizar la traducción.");
            }
            else
            {
                await DownloadAndUpdateFiles();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Si el usuario está minimizando, cancela el cierre
            if (this.WindowState == WindowState.Minimized)
            {
                e.Cancel = true; // Cancela el cierre
                this.Hide(); // Oculta la ventana
                notifyIcon.Visible = true; // Muestra el icono en la bandeja del sistema
            }
            base.OnClosing(e);
        }

        private void CloseApplicationButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void SearchDirectoriesButton_Click(object sender, RoutedEventArgs e)
        {
            var directorySelector = new DirectorySelectorWindow();
            if (directorySelector.ShowDialog() == true)
            {
                var selectedDirectory = directorySelector.SelectedDirectory;
                if (!string.IsNullOrEmpty(selectedDirectory))
                {
                    // Guardar la ubicación del directorio seleccionado manualmente
                    SaveSelectedDirectory(selectedDirectory);
                    Log($"Directorio seleccionado manualmente: {selectedDirectory}");
                }
            }
        }

        private void SaveSelectedDirectory(string directory)
        {
            var settings = new Dictionary<string, string>
            {
                { "SelectedDirectory", directory }
            };

            string json = JsonSerializer.Serialize(settings);
            File.WriteAllText("settings.json", json);
        }

        private string? LoadSelectedDirectory()
        {
            if (!File.Exists("settings.json"))
            {
                return null;
            }

            string json = File.ReadAllText("settings.json");
            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return settings?.GetValueOrDefault("SelectedDirectory");
        }
    }
}