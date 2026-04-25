using CodeStormHackathon.Services;
using System;
using System.IO;
using System.Windows;

namespace CodeStormHackathon.Views
{
    public partial class ModifyProjectWindow : Window
    {
        private readonly AIService _aiService = new AIService();

        public ModifyProjectWindow()
        {
            InitializeComponent();
        }

        private void BtnBrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Selectează Documentul de Modificat",
                Filter = "Fișiere Text/LaTeX (*.txt;*.tex)|*.txt;*.tex|Toate fișierele (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                TxtFilePath.Text = dlg.FileName;
            }
        }

        private async void BtnApplyChanges_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtFilePath.Text) || !File.Exists(TxtFilePath.Text))
            {
                MessageBox.Show("Te rog selectează un fișier valid.", "Atenție", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtInstructions.Text))
            {
                MessageBox.Show("Te rog scrie ce anume dorești să modifici în document.", "Atenție", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnApplyChanges.IsEnabled = false;
            LoadingSpinner.Visibility = Visibility.Visible;
            StatusText.Text = "AI-ul analizează și rescrie documentul... (te rugăm să aștepți)";

            try
            {
                // 1. Citim fișierul curent
                string fileContent = File.ReadAllText(TxtFilePath.Text);

                // 2. Apelăm AI-ul cu conținutul și instrucțiunile
                string modifiedContent = await _aiService.ModifyProjectFileAsync(fileContent, TxtInstructions.Text);

                // 3. Salvăm noul document
                string originalExt = Path.GetExtension(TxtFilePath.Text);
                var saveDlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Salvează Documentul Modificat",
                    Filter = $"Fișier (*{originalExt})|*{originalExt}",
                    FileName = $"Modificat_{Path.GetFileName(TxtFilePath.Text)}"
                };

                if (saveDlg.ShowDialog() == true)
                {
                    File.WriteAllText(saveDlg.FileName, modifiedContent);
                    StatusText.Text = "✅ Document rescris și salvat cu succes!";
                    MessageBox.Show("Fișierul a fost actualizat de AI și salvat!", "Succes", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = "Acțiune anulată.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "❌ Eroare la procesare.";
                MessageBox.Show($"Eroare: {ex.Message}", "Eroare AI", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnApplyChanges.IsEnabled = true;
                LoadingSpinner.Visibility = Visibility.Collapsed;
            }
        }
    }
}