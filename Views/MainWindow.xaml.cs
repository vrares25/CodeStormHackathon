using CodeStormHackathon.Models;
using CodeStormHackathon.Services;
using Microsoft.Win32;
using SistemAcademic.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace SistemAcademic
{
    public partial class MainWindow : Window
    {
        private readonly AIService _aiService;
        private readonly WordReaderService _wordReader;
        private readonly ValidationService _validationService;
        private readonly SyncService _syncService;
        private readonly TemplateShifterService _templateShifter;

        private string _extractedDocumentContext = "";
        private SyllabusData _currentExtractedData;

        public MainWindow()
        {
            InitializeComponent();

            _aiService = new AIService();
            _wordReader = new WordReaderService();
            _validationService = new ValidationService();
            _syncService = new SyncService();
            _templateShifter = new TemplateShifterService();

            // UX: Trimitere mesaj cu Enter
            ChatInput.KeyDown += ChatInput_KeyDown;
        }

        // =========================================================================
        // 1. SELECȚIE FIȘIERE & DRAG DROP (Logica Clean: Nume vs Cale)
        // =========================================================================

        private void BtnBrowsePlan_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "PDF Files (*.pdf)|*.pdf|Word Documents (*.docx)|*.docx" };
            if (openFileDialog.ShowDialog() == true)
            {
                TxtPlanPath.Text = System.IO.Path.GetFileName(openFileDialog.FileName);
                TxtPlanPath.Tag = openFileDialog.FileName;
            }
        }

        private void BtnBrowseFD_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "Documente (*.pdf;*.docx;*.jpg;*.png)|*.pdf;*.docx;*.jpg;*.png" };
            if (openFileDialog.ShowDialog() == true)
            {
                TxtFDPath.Text = System.IO.Path.GetFileName(openFileDialog.FileName);
                TxtFDPath.Tag = openFileDialog.FileName;
            }
        }

        private void FileDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effects = DragDropEffects.Copy;
            else e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void FileDropPlan(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files?.Length > 0)
                {
                    TxtPlanPath.Text = System.IO.Path.GetFileName(files[0]);
                    TxtPlanPath.Tag = files[0];
                }
            }
        }

        private void FileDropFD(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files?.Length > 0)
                {
                    TxtFDPath.Text = System.IO.Path.GetFileName(files[0]);
                    TxtFDPath.Tag = files[0];
                }
            }
        }

        // =========================================================================
        // 2. PROCESARE & VALIDARE (Folosind .Tag)
        // =========================================================================

        private void BtnFastCheck_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSelection(true)) return;

            ErrorsListBox.Items.Clear();
            StatusText.Text = "Verificare rapidă...";
            LoadingSpinner.Visibility = Visibility.Visible;

            try
            {
                string planText = ExtractTextFromFile(TxtPlanPath);
                string fdText = ExtractTextFromFile(TxtFDPath);

                _currentExtractedData = ManualParser(fdText);
                RunBusinessValidations(_currentExtractedData);
                UpdateGlobalContext(planText, fdText, _currentExtractedData);

                StatusText.Text = "Verificare finalizată.";
            }
            catch (Exception ex) { AddErrorMessage($"Eroare: {ex.Message}"); }
            finally { LoadingSpinner.Visibility = Visibility.Collapsed; }
        }

        private async void BtnAiAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSelection(true)) return;

            ErrorsListBox.Items.Clear();
            BtnAiAnalysis.IsEnabled = false;
            LoadingSpinner.Visibility = Visibility.Visible;

            try
            {
                string planText = ExtractTextFromFile(TxtPlanPath);
                string fdText = (RadioAiVision.IsChecked == true)
                    ? await _aiService.ExtractTextFromImageAsync(TxtFDPath.Tag.ToString())
                    : ExtractTextFromFile(TxtFDPath);

                StatusText.Text = "Gemma 3 extrage datele...";
                _currentExtractedData = await _aiService.ParseTextToDataAsync(fdText);

                RunBusinessValidations(_currentExtractedData);
                UpdateGlobalContext(planText, fdText, _currentExtractedData);

                StatusText.Text = "Generare raport semantic...";
                string report = await _aiService.AskCopilotAsync(_extractedDocumentContext, "Oferă un scurt raport de conformitate.");
                AddInfoMessage(report);

                StatusText.Text = "Analiză AI completă.";
            }
            catch (Exception ex) { AddErrorMessage($"Eroare AI: {ex.Message}"); }
            finally
            {
                BtnAiAnalysis.IsEnabled = true;
                LoadingSpinner.Visibility = Visibility.Collapsed;
            }
        }

        // =========================================================================
        // 3. CHAT UX (Enter & Bule Stilizate)
        // =========================================================================

        private void ChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                BtnSendCopilot_Click(sender, new RoutedEventArgs());
            }
        }

        private async void BtnSendCopilot_Click(object sender, RoutedEventArgs e)
        {
            string query = ChatInput.Text;
            if (string.IsNullOrWhiteSpace(query)) return;

            AddChatMessage("Tu", query);
            ChatInput.Text = "";
            BtnSendCopilot.IsEnabled = false;
            LoadingSpinner.Visibility = Visibility.Visible;

            try
            {
                string safeContext = string.IsNullOrEmpty(_extractedDocumentContext) ? "Fără documente încărcate încă." : _extractedDocumentContext;
                string response = await _aiService.AskCopilotAsync(safeContext, query);
                AddChatMessage("Gemma 3", response);
                StatusText.Text = "Gata.";
            }
            catch (Exception ex) { AddChatMessage("Sistem", $"Eroare: {ex.Message}"); }
            finally
            {
                BtnSendCopilot.IsEnabled = true;
                ChatInput.Focus();
                LoadingSpinner.Visibility = Visibility.Collapsed;
            }
        }

        // =========================================================================
        // 4. EXPORT
        // =========================================================================

        private void BtnExportSyllabus_Click(object sender, RoutedEventArgs e)
        {
            if (_currentExtractedData == null)
            {
                MessageBox.Show("Procesează o fișă înainte de export.");
                return;
            }

            try
            {
                string templatePath = "Template_FD_2026.docx";
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string output = System.IO.Path.Combine(desktop, $"Fișa_Nouă_{DateTime.Now:HHmm}.docx");

                _templateShifter.GenerateNewSyllabus(_currentExtractedData, templatePath, output);
                MessageBox.Show($"Fișier salvat pe Desktop:\n{output}", "Succes Export");
            }
            catch (Exception ex) { MessageBox.Show($"Eroare la export: {ex.Message}"); }
        }

        // =========================================================================
        // HELPERS
        // =========================================================================

        private void UpdateGlobalContext(string plan, string fd, SyllabusData data)
        {
            _extractedDocumentContext = $@"=== PLAN ===\n{plan}\n\n=== FISA ===\n{fd}\n\n=== ERORI ===\n{GetValidationErrorsAsString(data)}";
        }

        private string ExtractTextFromFile(TextBox target)
        {
            if (target.Tag == null) return "";
            string path = target.Tag.ToString();
            if (path.EndsWith(".docx")) return _wordReader.ExtractTextFromDocx(path);
            if (path.EndsWith(".pdf")) return _wordReader.ExtractTextFromPdfDirect(path);
            return "";
        }

        private void RunBusinessValidations(SyllabusData data)
        {
            // 1. Verificăm integritatea (Erorile 1 și 2: Biblio și Capitole)
            var errors = _validationService.CheckIntegrity(data);
            foreach (var err in errors) AddErrorMessage(err);

            // 2. Verificăm matematica (Eroarea 3: Ponderile 0%)
            if (!_validationService.ValidateWeights(data, out string math))
                AddErrorMessage(math);

            // 3. Comparăm cu Planul de Învățământ (Eroarea 4: Conflictul de evaluare)
            // Momentan folosim un mockPlan, dar aici va veni logica de comparare reală
            var mockPlan = new StudyPlanEntry { Credits = 5, EvaluationType = "Examen" };
            var conflicts = _syncService.CompareWithPlan(data, mockPlan);
            foreach (var c in conflicts)
                AddErrorMessage($"[CONFLICT]: {c}");
        }

        private string GetValidationErrorsAsString(SyllabusData data)
        {
            var errors = _validationService.CheckIntegrity(data);
            if (!_validationService.ValidateWeights(data, out string math)) errors.Add(math);
            return errors.Any() ? string.Join("\n", errors) : "OK";
        }

        private SyllabusData ManualParser(string text) => new SyllabusData { SubjectName = "Curs Extras", Credits = 5 };

        private bool ValidateSelection(bool both = false)
        {
            if (both && (TxtPlanPath.Tag == null || TxtFDPath.Tag == null))
            {
                MessageBox.Show("Selectează ambele documente!");
                return false;
            }
            return true;
        }

        private void AddErrorMessage(string msg) => AddStatusItem(msg, PackIconKind.AlertCircleOutline, Brushes.Red);
        private void AddInfoMessage(string msg) => AddStatusItem(msg, PackIconKind.Brain, Brushes.Blue);
        private void AddSuccessMessage(string msg) => AddStatusItem(msg, PackIconKind.CheckCircleOutline, Brushes.Green);

        private void AddStatusItem(string msg, PackIconKind icon, Brush color)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            panel.Children.Add(new PackIcon { Kind = icon, Foreground = color, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(new TextBlock { Text = msg, Foreground = color, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
            ErrorsListBox.Items.Add(panel);
        }

        private void AddChatMessage(string sender, string msg)
        {
            bool isUser = sender == "Tu";
            var bubble = new Border
            {
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 5, 0, 10),
                MaxWidth = 260,
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                CornerRadius = isUser ? new CornerRadius(15, 15, 2, 15) : new CornerRadius(15, 15, 15, 2),
                Background = isUser ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"))
                                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB"))
            };
            bubble.Child = new TextBlock
            {
                Text = msg,
                TextWrapping = TextWrapping.Wrap,
                Foreground = isUser ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937"))
            };
            ChatDisplay.Children.Add(bubble);
            (ChatDisplay.Parent as ScrollViewer)?.ScrollToBottom();
        }
    }
}