using CodeStormHackathon.Models;
using CodeStormHackathon.Services;
using Microsoft.Win32;
using CodeStormHackathon.Models;
using SistemAcademic.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SistemAcademic
{
    public partial class MainWindow : Window
    {
        private readonly AIService _aiService;
        private readonly WordReaderService _wordReader;
        private readonly ValidationService _validationService;
        private readonly SyncService _syncService;
        private readonly TemplateShifterService _templateShifter;

        // Contextul global care va fi trimis către AI (conține Planul, Fișa și Erorile)
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
        }

        // =========================================================================
        // 1. SELECȚIE FIȘIERE
        // =========================================================================

        private void BtnBrowsePlan_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "PDF Files (*.pdf)|*.pdf|Word Documents (*.docx)|*.docx" };
            if (openFileDialog.ShowDialog() == true) TxtPlanPath.Text = openFileDialog.FileName;
        }

        private void BtnBrowseFD_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "Documente (*.pdf;*.docx;*.jpg;*.png)|*.pdf;*.docx;*.jpg;*.png" };
            if (openFileDialog.ShowDialog() == true) TxtFDPath.Text = openFileDialog.FileName;
        }

        // =========================================================================
        // 2. VERIFICARE RAPIDĂ (FĂRĂ AI)
        // =========================================================================

        private void BtnFastCheck_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSelection(true)) return;

            ErrorsListBox.Items.Clear();
            StatusText.Text = "Verificare logică rapidă în curs...";

            try
            {
                // Citim ambele documente digital
                string planText = ExtractTextFromFile(TxtPlanPath.Text);
                string fdText = ExtractTextFromFile(TxtFDPath.Text);

                // Parsare fără AI (folosind logica din WordReader)
                _currentExtractedData = ManualParser(fdText);

                // Rulăm validările de business
                RunBusinessValidations(_currentExtractedData);

                // Construim contextul pentru chat în caz că profesorul întreabă ceva
                UpdateGlobalContext(planText, fdText, _currentExtractedData);

                StatusText.Text = "Verificare rapidă finalizată.";
            }
            catch (Exception ex)
            {
                AddErrorMessage($"Eroare procesare: {ex.Message}");
            }
        }

        // =========================================================================
        // 3. ANALIZĂ AVANSATĂ (CU AI - GEMMA 3 & VISION)
        // =========================================================================

        private async void BtnAiAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSelection(true)) return;

            ErrorsListBox.Items.Clear();
            BtnAiAnalysis.IsEnabled = false;
            StatusText.Text = "AI-ul analizează documentele...";

            try
            {
                string planText = ExtractTextFromFile(TxtPlanPath.Text);
                string fdText = "";

                // Decidem dacă folosim Vision sau Parser Digital
                if (RadioAiVision.IsChecked == true)
                {
                    StatusText.Text = "Procesare imagine cu Llama 3.2 Vision...";
                    fdText = await _aiService.ExtractTextFromImageAsync(TxtFDPath.Text);
                }
                else
                {
                    fdText = ExtractTextFromFile(TxtFDPath.Text);
                }

                // Gemma 3 extrage datele structurate din Fișă
                StatusText.Text = "Gemma 3 extrage datele JSON...";
                _currentExtractedData = await _aiService.ParseTextToDataAsync(fdText);

                // Validăm datele extrase
                RunBusinessValidations(_currentExtractedData);

                // Actualizăm contextul global cu tot ce am aflat
                UpdateGlobalContext(planText, fdText, _currentExtractedData);

                // Cerem un raport semantic inițial de la Gemma 3
                StatusText.Text = "Generare raport semantic...";
                string report = await _aiService.AskCopilotAsync(_extractedDocumentContext, "Analizează aceste date și oferă un scurt raport despre conformitate.");

                AddInfoMessage("--- RAPORT SEMANTIC GEMMA 3 ---");
                AddInfoMessage(report);

                StatusText.Text = "Analiză AI completă.";
            }
            catch (Exception ex)
            {
                AddErrorMessage($"Eroare AI: {ex.Message}");
            }
            finally
            {
                BtnAiAnalysis.IsEnabled = true;
            }
        }

        // =========================================================================
        // 4. ACADEMIC COPILOT (CHAT) - FOLOSEȘTE CONTEXTUL COMPLET
        // =========================================================================

        private async void BtnSendCopilot_Click(object sender, RoutedEventArgs e)
        {
            string query = ChatInput.Text;
            if (string.IsNullOrWhiteSpace(query)) return;

            if (string.IsNullOrEmpty(_extractedDocumentContext))
            {
                AddChatMessage("Sistem", "⚠️ Încarcă documentele și rulează o analiză pentru a oferi context asistentului.");
                return;
            }

            AddChatMessage("Tu", query);
            ChatInput.Text = "";
            BtnSendCopilot.IsEnabled = false;

            try
            {
                StatusText.Text = "Gemma 3 procesează...";
                string response = await _aiService.AskCopilotAsync(_extractedDocumentContext, query);
                AddChatMessage("Gemma 3", response);
                StatusText.Text = "Gata.";
            }
            catch (Exception ex)
            {
                AddChatMessage("Sistem", $"Eroare API: {ex.Message}");
            }
            finally
            {
                BtnSendCopilot.IsEnabled = true;
            }
        }

        // =========================================================================
        // 5. EXPORT TEMPLATE
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
            catch (Exception ex)
            {
                MessageBox.Show($"Eroare la export: {ex.Message}");
            }
        }

        // =========================================================================
        // HELPERS
        // =========================================================================

        private void UpdateGlobalContext(string plan, string fd, SyllabusData data)
        {
            _extractedDocumentContext = $@"
                === PLAN DE ÎNVĂȚĂMÂNT (SURSA DE ADEVĂR) ===
                {plan}

                === FIȘA DISCIPLINEI CURENTĂ ===
                {fd}

                === ERORI DETECTATE DE SISTEM ===
                {GetValidationErrorsAsString(data)}
            ";
        }

        private string ExtractTextFromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Contains("Selectează")) return "";
            if (path.EndsWith(".docx")) return _wordReader.ExtractTextFromDocx(path);
            if (path.EndsWith(".pdf")) return "Conținut extras din " + System.IO.Path.GetFileName(path);
            return "";
        }

        private void RunBusinessValidations(SyllabusData data)
        {
            var errors = _validationService.CheckIntegrity(data);
            foreach (var err in errors) AddErrorMessage(err);

            string math;
            if (!_validationService.ValidateWeights(data, out math)) AddErrorMessage(math);

            var mockPlan = new StudyPlanEntry { Credits = 5, EvaluationType = "Examen" };
            var conflicts = _syncService.CompareWithPlan(data, mockPlan);
            foreach (var c in conflicts) AddErrorMessage($"[CONFLICT]: {c}");
        }

        private string GetValidationErrorsAsString(SyllabusData data)
        {
            var errors = _validationService.CheckIntegrity(data);
            string math;
            if (!_validationService.ValidateWeights(data, out math)) errors.Add(math);
            return errors.Any() ? string.Join("\n", errors) : "Nicio eroare detectată.";
        }

        private SyllabusData ManualParser(string text) => new SyllabusData { SubjectName = "Curs Extras Digital", Credits = 5 };

        private bool ValidateSelection(bool both = false)
        {
            if (both && (TxtPlanPath.Text.Contains("Selectează") || TxtFDPath.Text.Contains("Selectează")))
            {
                MessageBox.Show("Selectează ambele documente!");
                return false;
            }
            return true;
        }

        private void AddErrorMessage(string msg) => ErrorsListBox.Items.Add(new TextBlock { Text = $"❌ {msg}", Foreground = Brushes.Red, TextWrapping = TextWrapping.Wrap });
        private void AddInfoMessage(string msg) => ErrorsListBox.Items.Add(new TextBlock { Text = $"🧠 {msg}", Foreground = Brushes.Blue, FontStyle = FontStyles.Italic, TextWrapping = TextWrapping.Wrap });
        private void AddSuccessMessage(string msg) => ErrorsListBox.Items.Add(new TextBlock { Text = $"✅ {msg}", Foreground = Brushes.Green });

        private void AddChatMessage(string sender, string msg)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 10) };
            panel.Children.Add(new TextBlock { Text = sender, FontWeight = FontWeights.Bold, Foreground = (sender == "Tu" ? Brushes.DarkSlateGray : Brushes.Indigo) });
            panel.Children.Add(new TextBlock { Text = msg, TextWrapping = TextWrapping.Wrap });

            ChatDisplay.Children.Add(panel);
        }
    }
}