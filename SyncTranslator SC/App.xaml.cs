using System;
using System.Media;
using System.Windows;

namespace SyncTranslator_SC
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        // ...existing code...
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Reproducir el sonido de inicio
            try
            {
                var player = new SoundPlayer("inicio.wav"); // Cambiar a un archivo WAV válido
                player.Play();
            }
            catch (Exception ex)
            {
                // Manejar cualquier error que ocurra al reproducir el sonido
                System.Windows.MessageBox.Show($"Error al reproducir el sonido de inicio: {ex.Message}");
            }
        }
    }
}