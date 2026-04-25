using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xceed.Words.NET;

namespace CodeStormHackathon.Views
{
    public partial class BulkUpdateWindow : Window
    {
        public ObservableCollection<BulkFileItem> FilesToProcess { get; set; } = new ObservableCollection<BulkFileItem>();

        public BulkUpdateWindow()
        {
            InitializeComponent();
            FileList.ItemsSource = FilesToProcess;
        }

        private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Word Documents (*.docx)|*.docx",
                Title = "Selectează Fișele de Disciplină pentru actualizare"
            };

            if (dlg.ShowDialog() == true)
            {
                foreach (var file in dlg.FileNames)
                {
                    FilesToProcess.Add(new BulkFileItem
                    {
                        FilePath = file,
                        FileName = Path.GetFileName(file),
                        Status = "În așteptare",
                        StatusColor = Brushes.SlateGray
                    });
                }
            }
        }

        private void BtnApplyAll_Click(object sender, RoutedEventArgs e)
        {
            string searchTxt = TxtSearch.Text;
            string replaceTxt = TxtReplace.Text;

            if (string.IsNullOrEmpty(searchTxt))
            {
                MessageBox.Show("Introduceți textul de căutat!", "Atenție", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (FilesToProcess.Count == 0)
            {
                MessageBox.Show("Adăugați cel puțin un fișier!", "Atenție", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int successCount = 0;

            foreach (var item in FilesToProcess)
            {
                try
                {
                    // Utilizăm DocX pentru a deschide și modifica fișierul Word
                    using (var document = DocX.Load(item.FilePath))
                    {
                        // Înlocuim textul. DocX face asta fără să strice formatarea!
                        document.ReplaceText(searchTxt, replaceTxt);
                        document.Save();
                    }

                    item.Status = "✅ Actualizat cu succes";
                    item.StatusColor = Brushes.Green;
                    successCount++;
                }
                catch (Exception ex)
                {
                    item.Status = $"❌ Eroare: {ex.Message}";
                    item.StatusColor = Brushes.Red;
                }
            }

            // Forțăm interfața să se actualizeze
            FileList.Items.Refresh();
            MessageBox.Show($"Au fost actualizate {successCount} din {FilesToProcess.Count} documente.", "Operațiune Finalizată", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // Model de date simplu pentru a popula lista din interfață
    public class BulkFileItem
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Status { get; set; }
        public Brush StatusColor { get; set; }
    }
}