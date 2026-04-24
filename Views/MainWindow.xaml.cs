using CodeStormHackathon.Models;
using SistemAcademic.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;

namespace CodeStormHackathon.Views
{
    public partial class MainWindow : Window
    {
        private readonly AIService _aiService = new AIService();
        private List<SyllabusData> _extractedSubjectsList;
        private string _chatContext = "";

        public MainWindow() { InitializeComponent(); }

        private async void BtnAiAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtPlanPath.Text) || string.IsNullOrEmpty(TxtFDPath.Text)) return;

            ErrorsListBox.Items.Clear();
            BtnAiAnalysis.IsEnabled = false;
            LoadingSpinner.Visibility = Visibility.Visible;
            StatusText.Text = "AI analizează documentele...";

            try
            {
                _extractedSubjectsList = await _aiService.ExtractAllSyllabusDataAsync(TxtFDPath.Text);

                foreach (var subject in _extractedSubjectsList)
                {
                    AddSuccessMessage($"Audit pentru: {subject.SubjectName}");
                    var planData = await _aiService.ExtractFromStudyPlanAsync(TxtPlanPath.Text, subject.SubjectName);

                    if (subject.Credits != planData.Credits)
                        AddErrorMessage($"[Sincronizare] Credite diferite! Fișă: {subject.Credits} vs Plan: {planData.Credits}");
                    else
                        AddSuccessMessage($"[Sincronizare] Credite OK ({subject.Credits}).");
                }
                _chatContext = JsonConvert.SerializeObject(_extractedSubjectsList);
                StatusText.Text = "Analiză completă.";
            }
            catch (Exception ex) { AddErrorMessage(ex.Message); }
            finally { BtnAiAnalysis.IsEnabled = true; LoadingSpinner.Visibility = Visibility.Collapsed; }
        }

        private async void BtnSendCopilot_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ChatInput.Text)) return;
            string q = ChatInput.Text; ChatInput.Text = "";
            AddChatMessage("Tu", q);
            string resp = await _aiService.AskCopilotAsync(_chatContext, q);
            AddChatMessage("AI", resp);
        }

        private void AddErrorMessage(string m) => ErrorsListBox.Items.Add(new System.Windows.Controls.TextBlock { Text = "❌ " + m, Foreground = Brushes.Red });
        private void AddSuccessMessage(string m) => ErrorsListBox.Items.Add(new System.Windows.Controls.TextBlock { Text = "✅ " + m, Foreground = Brushes.Green });

        private void AddChatMessage(string s, string m)
        {
            var p = new System.Windows.Controls.StackPanel { Margin = new Thickness(5) };
            p.Children.Add(new System.Windows.Controls.TextBlock { Text = s, FontWeight = FontWeights.Bold });
            p.Children.Add(new System.Windows.Controls.TextBlock { Text = m, TextWrapping = TextWrapping.Wrap });
            ChatDisplay.Children.Add(p);
        }

        // Drag and Drop
        private void FileDragOver(object s, DragEventArgs e) => e.Effects = DragDropEffects.Copy;
        private void FileDropPlan(object s, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) TxtPlanPath.Text = ((string[])e.Data.GetData(DataFormats.FileDrop))[0]; }
        private void FileDropFD(object s, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) TxtFDPath.Text = ((string[])e.Data.GetData(DataFormats.FileDrop))[0]; }

        private void BtnExportSyllabus_Click(object s, RoutedEventArgs e) { /* Logica de export rămâne neschimbată */ }
        private void BtnBrowsePlan_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog { Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*" };
            if (openFileDialog.ShowDialog() == true)
            {
                TxtPlanPath.Text = openFileDialog.FileName;
            }
        }

        private void BtnBrowseFD_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog { Filter = "Academic Documents (*.pdf;*.docx)|*.pdf;*.docx|Images (*.jpg;*.png)|*.jpg;*.png" };
            if (openFileDialog.ShowDialog() == true)
            {
                TxtFDPath.Text = openFileDialog.FileName;
            }
        }
    }
}