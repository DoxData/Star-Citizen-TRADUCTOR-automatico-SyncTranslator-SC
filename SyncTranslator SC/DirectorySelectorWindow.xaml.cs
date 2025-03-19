using System.Windows;
using System.IO;

namespace SyncTranslator_SC
{
    public partial class DirectorySelectorWindow : Window
    {
        public string SelectedDirectory { get; private set; } = string.Empty;

        public DirectorySelectorWindow()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DirectoryTextBox.Text = dialog.SelectedPath;
            }
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedDirectory = DirectoryTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DeleteSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            string settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(settingsFilePath))
            {
                File.Delete(settingsFilePath);
                System.Windows.MessageBox.Show("El archivo settings.json ha sido eliminado.", "Configuración Eliminada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("El archivo settings.json no existe.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
