using CodeStormHackathon.Services;
using System;
using System.IO;
using System.Windows;

namespace CodeStormHackathon.Views
{
    public partial class TemplateFillerWindow : Window
    {
        private readonly AIService _aiService = new AIService();
        private readonly PdfService _pdfService = new PdfService();
        private readonly WordReaderService _wordReader = new WordReaderService();

        public TemplateFillerWindow()
        {
            InitializeComponent();
        }

        private void BtnBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Selectează Documentul Sursă" };
            if (dlg.ShowDialog() == true) TxtSourcePath.Text = dlg.FileName;
        }

        private void BtnBrowseTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Selectează Șablonul LaTeX", Filter = "Text/LaTeX (*.txt;*.tex)|*.txt;*.tex" };
            if (dlg.ShowDialog() == true) TxtTemplatePath.Text = dlg.FileName;
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtSourcePath.Text) || string.IsNullOrEmpty(TxtTemplatePath.Text))
            {
                MessageBox.Show("Vă rugăm să selectați ambele fișiere.", "Eroare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnGenerate.IsEnabled = false;
            LoadingSpinner.Visibility = Visibility.Visible;
            StatusText.Text = "Extragere text și procesare AI în curs... (poate dura până la 1 minut)";

            try
            {
                // 1. Citește direct textul din șablonul tău LaTeX
                string templateText = File.ReadAllText(TxtTemplatePath.Text);

                // 2. Apelează noul serviciu multimodal (îi dăm calea PDF-ului și textul șablonului)
                string filledLatex = await _aiService.FillLatexTemplateFromVisionAsync(TxtSourcePath.Text, templateText);

                // 3. Salvează fișierul rezultat
                var saveDlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "LaTeX File (*.tex)|*.tex",
                    FileName = "Plan_Generat_AI.tex"
                };

                if (saveDlg.ShowDialog() == true)
                {
                    File.WriteAllText(saveDlg.FileName, filledLatex);
                    StatusText.Text = "✅ Document generat și salvat cu succes!";
                    MessageBox.Show("Fișierul LaTeX a fost generat și datele au fost mapate vizual!", "Succes", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = "Anulat de utilizator.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "❌ Eroare la generare.";
                MessageBox.Show($"A apărut o eroare: {ex.Message}", "Eroare", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnGenerate.IsEnabled = true;
                LoadingSpinner.Visibility = Visibility.Collapsed;
            }
        }
    }
}