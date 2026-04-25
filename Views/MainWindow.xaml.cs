using CodeStormHackathon.Models;
using CodeStormHackathon.Services;
using Newtonsoft.Json;
using SistemAcademic.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace CodeStormHackathon.Views
{
    public partial class MainWindow : Window
    {
        private readonly AIService _aiService = new AIService();
        private readonly ValidationService _validationService = new ValidationService();
        private readonly SyncService _syncService = new SyncService();

        private List<SyllabusData> _extractedSubjectsList;
        private string _chatContext = "";

        public MainWindow() { InitializeComponent(); }

        // ─────────────────────────────────────────────────────────────────
        // Buton principal: EXECUTE AI ACADEMIC AUDIT
        // Rulează Level 1 + Level 2 pentru toate materiile extrase
        // ─────────────────────────────────────────────────────────────────
        private async void BtnAiAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtPlanPath.Text) || string.IsNullOrEmpty(TxtFDPath.Text))
            {
                MessageBox.Show("Te rog selectează ambele fișiere înainte de a rula auditul.",
                    "Fișiere lipsă", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ErrorsListBox.Items.Clear();
            BtnAiAnalysis.IsEnabled = false;
            LoadingSpinner.Visibility = Visibility.Visible;
            StatusText.Text = "AI extrage date din documente...";

            try
            {
                // ── Pasul 1: Extrage toate materiile din Fișa Disciplinei ──
                _extractedSubjectsList = await _aiService.ExtractAllSyllabusDataAsync(TxtFDPath.Text);

                AddSectionHeader($"── Găsite {_extractedSubjectsList.Count} discipline în FD ──");

                foreach (var subject in _extractedSubjectsList)
                {
                    AddSectionHeader($"📋 {subject.SubjectName}");

                    // ── Level 1: Validare structurală și matematică ──
                    StatusText.Text = $"Level 1: Validare integritate — {subject.SubjectName}...";
                    var l1Results = _validationService.RunAllChecks(subject);
                    DisplayResults(l1Results);

                    // ── Level 2: Sincronizare cu Planul de Învățământ ──
                    StatusText.Text = $"Level 2: Sincronizare cu Planul — {subject.SubjectName}...";
                    var planEntry = await _aiService.ExtractFromStudyPlanAsync(
                        TxtPlanPath.Text, subject.SubjectName);

                    var l2Results = _syncService.RunAllChecks(subject, planEntry);
                    DisplayResults(l2Results);
                }

                // ── Actualizează contextul pentru Copilot ──
                _chatContext = JsonConvert.SerializeObject(_extractedSubjectsList, Formatting.Indented);
                StatusText.Text = $"✅ Audit complet — {_extractedSubjectsList.Count} discipline procesate.";
            }
            catch (Exception ex)
            {
                AddResult(ValidationResult.Error("SISTEM", $"Eroare neașteptată: {ex.Message}"));
                StatusText.Text = "❌ Eroare la procesare.";
            }
            finally
            {
                BtnAiAnalysis.IsEnabled = true;
                LoadingSpinner.Visibility = Visibility.Collapsed;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Afișează o listă de ValidationResult în ListBox cu culori
        // ─────────────────────────────────────────────────────────────────
        private void DisplayResults(List<ValidationResult> results)
        {
            foreach (var result in results)
                AddResult(result);
        }

        private void AddResult(ValidationResult result)
        {
            var tb = new System.Windows.Controls.TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 1, 2, 1)
            };

            switch (result.Severity)
            {
                case ValidationSeverity.Error:
                    tb.Text = $"❌ [{result.UseCase}] {result.Message}";
                    tb.Foreground = Brushes.Red;
                    break;
                case ValidationSeverity.Warning:
                    tb.Text = $"⚠️ [{result.UseCase}] {result.Message}";
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(200, 130, 0));
                    break;
                case ValidationSeverity.Success:
                    tb.Text = $"✅ [{result.UseCase}] {result.Message}";
                    tb.Foreground = Brushes.Green;
                    break;
            }

            ErrorsListBox.Items.Add(tb);
        }

        private void AddSectionHeader(string text)
        {
            var tb = new System.Windows.Controls.TextBlock
            {
                Text = text,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                Margin = new Thickness(2, 8, 2, 2)
            };
            ErrorsListBox.Items.Add(tb);
        }

        // ─────────────────────────────────────────────────────────────────
        // AI Copilot
        // ─────────────────────────────────────────────────────────────────
        private async void BtnSendCopilot_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ChatInput.Text)) return;

            string q = ChatInput.Text;
            ChatInput.Text = "";
            AddChatMessage("Tu", q);

            BtnSendCopilot.IsEnabled = false;
            try
            {
                string resp = await _aiService.AskCopilotAsync(_chatContext, q);
                AddChatMessage("AI", resp);
            }
            catch (Exception ex)
            {
                AddChatMessage("Eroare", ex.Message);
            }
            finally
            {
                BtnSendCopilot.IsEnabled = true;
            }
        }

        private void AddChatMessage(string sender, string message)
        {
            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(5) };

            var senderBlock = new System.Windows.Controls.TextBlock
            {
                Text = sender,
                FontWeight = FontWeights.Bold,
                Foreground = sender == "Tu"
                    ? new SolidColorBrush(Color.FromRgb(99, 102, 241))
                    : new SolidColorBrush(Color.FromRgb(16, 185, 129))
            };

            var messageBlock = new System.Windows.Controls.TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            };

            panel.Children.Add(senderBlock);
            panel.Children.Add(messageBlock);
            ChatDisplay.Children.Add(panel);
        }

        // ─────────────────────────────────────────────────────────────────
        // Export / Migrare pe template nou (Level 4)
        // ─────────────────────────────────────────────────────────────────
        private void BtnExportSyllabus_Click(object sender, RoutedEventArgs e)
        {
            if (_extractedSubjectsList == null || _extractedSubjectsList.Count == 0)
            {
                MessageBox.Show("Rulează mai întâi auditul AI pentru a extrage datele.",
                    "Date lipsă", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Word Documents (*.docx)|*.docx",
                FileName = $"FD_Migrat_{DateTime.Now:yyyy-MM-dd}.docx"
            };

            if (saveDialog.ShowDialog() != true) return;

            // Caută template-ul în directorul aplicației
            string templatePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Template_FD_2026.docx");

            if (!File.Exists(templatePath))
            {
                MessageBox.Show($"Template-ul nu a fost găsit la:\n{templatePath}\n\n" +
                    "Plasează fișierul Template_FD_2026.docx în directorul aplicației.",
                    "Template lipsă", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var shifter = new TemplateShifterService();
                // Ia prima materie extrasă (sau poți face un dialog de selecție)
                shifter.GenerateNewSyllabus(_extractedSubjectsList[0], templatePath, saveDialog.FileName);
                StatusText.Text = $"✅ Document migrat: {Path.GetFileName(saveDialog.FileName)}";
                MessageBox.Show($"Fișa a fost migrată cu succes!\n{saveDialog.FileName}",
                    "Export reușit", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Eroare la export: {ex.Message}", "Eroare",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Drag & Drop + Browse
        // ─────────────────────────────────────────────────────────────────
        private void FileDragOver(object s, DragEventArgs e) =>
            e.Effects = DragDropEffects.Copy;

        private void FileDropPlan(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                TxtPlanPath.Text = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
        }

        private void FileDropFD(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                TxtFDPath.Text = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
        }

        private void BtnBrowsePlan_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
                TxtPlanPath.Text = dlg.FileName;
        }

        private void BtnBrowseFD_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Academic Documents (*.pdf;*.docx)|*.pdf;*.docx|Images (*.jpg;*.png)|*.jpg;*.png"
            };
            if (dlg.ShowDialog() == true)
                TxtFDPath.Text = dlg.FileName;
        }

        private void BtnOpenDiff_Click(object sender, RoutedEventArgs e)
        {
            // VARIANTA A: Dacă există deja date extrase din audit,
            // trimitem prima disciplină extrasă și deschidem fereastra direct (fără AI extra)
            if (_extractedSubjectsList != null && _extractedSubjectsList.Count >= 2)
            {
                // Comparăm prima cu a doua disciplină extrasă (ex: 2 ani diferiți)
                var diffWindow = new DiffWindow(
                    _extractedSubjectsList[0],
                    _extractedSubjectsList[1]);
                diffWindow.Show();
            }
            else
            {
                // VARIANTA B: Deschidem fereastra goală — userul selectează 2 fișiere manual
                var diffWindow = new DiffWindow();
                diffWindow.Show();
            }
        }
    }
}