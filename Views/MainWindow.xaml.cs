using CodeStormHackathon.Models;
using CodeStormHackathon.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodeStormHackathon
{
    public partial class MainWindow : Window
    {
        private string _extractedDocumentContext = "";
        private SyllabusData _currentExtractedData;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnBrowsePlan_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf|Word Documents (*.docx)|*.docx",
                Title = "Selectează Planul de Învățământ"
            };
            if (openFileDialog.ShowDialog() == true)
                TxtPlanPath.Text = openFileDialog.FileName;
        }
        private void BtnBrowseFD_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Word Documents (*.docx)|*.docx|Images (*.jpg, *.png)|*.jpg;*.png",
                Title = "Selectează Fișa Disciplinei"
            };
            if (openFileDialog.ShowDialog() == true)
                TxtFDPath.Text = openFileDialog.FileName;
        }
        private async void BtnRunChecks_Click(object sender, RoutedEventArgs e)
        {
            ErrorsListBox.Items.Clear();

            if (string.IsNullOrEmpty(TxtPlanPath.Text) || TxtPlanPath.Text.Contains("Calea") ||
                string.IsNullOrEmpty(TxtFDPath.Text) || TxtFDPath.Text.Contains("Calea"))
            {
                MessageBox.Show("Te rog selectează ambele fișiere!", "Atenție");
                return;
            }

            try
            {
                StatusText.Text = "Se procesează...";

                var wordReader = new WordReaderService();
                var aiService = new AIService();
                var validator = new ValidationService();
                var sync = new SyncService();

                var officialPlan = new StudyPlanEntry { SubjectName = "Sisteme Inteligente", Credits = 5, EvaluationType = "Examen" };

                string rawText = "";
                string path = TxtFDPath.Text.ToLower();

                if (path.EndsWith(".jpg") || path.EndsWith(".png") || path.EndsWith(".jpeg"))
                {
                    StatusText.Text = "AI Vision: Extragere text din imagine...";
                    rawText = await aiService.ExtractTextFromImageAsync(TxtFDPath.Text);
                }
                else
                {
                    rawText = wordReader.ExtractTextFromDocx(TxtFDPath.Text);
                }

                _extractedDocumentContext = rawText;

                StatusText.Text = "AI: Analiză structură document...";
                _currentExtractedData = await aiService.ParseTextToDataAsync(rawText);

                var results = new List<string>();
                results.AddRange(validator.CheckIntegrity(_currentExtractedData));

                if (!validator.ValidateWeights(_currentExtractedData, out string mathMsg))
                    results.Add(mathMsg);

                results.AddRange(sync.CompareWithPlan(_currentExtractedData, officialPlan));
                if (results.Count == 0)
                    ErrorsListBox.Items.Add("✅ Totul este corect!");
                else
                    foreach (var err in results) ErrorsListBox.Items.Add(err);

                StatusText.Text = "Procesare completă. Poți folosi Copilotul sau Genera Fișa Nouă.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Eroare la procesare: {ex.Message}", "Eroare", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Eroare.";
            }
        }
        private async void BtnSendCopilot_Click(object sender, RoutedEventArgs e)
        {
            string userQuery = ChatInput.Text;

            if (string.IsNullOrWhiteSpace(userQuery)) return;
            if (string.IsNullOrEmpty(_extractedDocumentContext))
            {
                MessageBox.Show("Procesează un document mai întâi pentru context!", "Lipsă Context");
                return;
            }
            ChatInput.IsEnabled = false;
            BtnSendCopilot.IsEnabled = false;
            ChatHistory.Text += $"\n\nTu: {userQuery}";
            ChatInput.Text = "";
            try
            {
                StatusText.Text = "Gemma 2 generează răspunsul...";
                var aiService = new AIService();
                string aiResponse = await aiService.AskCopilotAsync(_extractedDocumentContext, userQuery);
                ChatHistory.Foreground = Brushes.Black;
                ChatHistory.Text += $"\n\nCopilot: {aiResponse}";
                var viewer = ChatHistory.Parent as ScrollViewer;
                viewer?.ScrollToBottom();
            }
            catch (Exception ex)
            {
                ChatHistory.Text += $"\n\nEroare AI: {ex.Message}";
            }
            finally
            {
                ChatInput.IsEnabled = true;
                BtnSendCopilot.IsEnabled = true;
                StatusText.Text = "Gata.";
            }
        }
        private void BtnExportSyllabus_Click(object sender, RoutedEventArgs e)
        {
            if (_currentExtractedData == null)
            {
                MessageBox.Show("Nu există date procesate pentru export!", "Avertisment");
                return;
            }

            try
            {
                string templatePath = @"C:\Hackathon\Template_FD_2026.docx";
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string outputPath = Path.Combine(desktopPath, $"FD_2026_{_currentExtractedData.SubjectName}.docx");

                var templateShifter = new TemplateShifterService();
                templateShifter.GenerateNewSyllabus(_currentExtractedData, templatePath, outputPath);

                MessageBox.Show($"Migrare reușită! Fișier salvat pe Desktop:\n{Path.GetFileName(outputPath)}", "Succes");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Eroare la Export");
            }
        }
    }
}