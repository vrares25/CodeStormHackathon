using CodeStormHackathon.Models;
using CodeStormHackathon.Services;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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

        public MainWindow()
        {
            InitializeComponent();

            ShowEmptyState("Încarcă documentele și rulează un audit pentru a vedea raportul de conformitate.",
                           PackIconKind.FileDocumentOutline, Brushes.LightGray);

            ShowChatEmptyState();

            ChatInput.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    e.Handled = true;
                    BtnSendCopilot_Click(s, new RoutedEventArgs());
                }
            };
        }
        private void BtnOpenLatexFiller_Click(object sender, RoutedEventArgs e)
        {
            var latexWindow = new TemplateFillerWindow();
            latexWindow.Show();
        }
        // ─────────────────────────────────────────────────────────────────
        // Buton principal: EXECUTE AI ACADEMIC AUDIT
        // Rulează Level 1 + Level 2 pentru toate materiile extrase
        // ─────────────────────────────────────────────────────────────────
        private async void BtnAiAnalysis_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validare inițială
            if (TxtPlanPath.Tag == null || TxtFDPath.Tag == null)
            {
                MessageBox.Show("Te rog selectează ambele fișiere înainte de a rula auditul.",
                    "Fișiere lipsă", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Pregătire UI: Curățăm lista și adăugăm Cardul de Sumar
            ErrorsListBox.Items.Clear();
            var summaryCard = CreateSummaryCard("🔍 ANALIZĂ ÎN CURS...");
            ErrorsListBox.Items.Add(summaryCard);

            BtnAiAnalysis.IsEnabled = false;
            LoadingSpinner.Visibility = Visibility.Visible;
            StatusText.Text = "AI extrage date din documente...";

            try
            {
                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                string planFullPath = TxtPlanPath.Tag.ToString();
                string fdFullPath = TxtFDPath.Tag.ToString();

                // ── PASUL 1: Extracție AI ──
                _extractedSubjectsList = await _aiService.ExtractAllSyllabusDataAsync(fdFullPath);

                // Actualizăm textul din cardul de sumar
                ((summaryCard.Child as StackPanel).Children[0] as TextBlock).Text =
                    $"📊 AUDIT FINALIZAT: {_extractedSubjectsList.Count} DISCIPLINE ANALIZATE";

                // ★ NOU: Pregătim contextul textual detaliat pentru Copilot
                System.Text.StringBuilder contextBuilder = new System.Text.StringBuilder();
                contextBuilder.AppendLine("RAPORT DE AUDIT ACADEMIC GENERAT:");

                foreach (var subject in _extractedSubjectsList)
                {
                    // ── PASUL 2: Header Stilizat pentru Materie ──
                    AddSectionHeader($"📋 {subject.SubjectName}");

                    // Colectăm toate rezultatele într-o singură listă pentru a decide starea vizuală
                    var allResults = new List<CodeStormHackathon.Services.ValidationResult>();

                    // Level 1: Integritate & Math
                    StatusText.Text = $"Validare Level 1 — {subject.SubjectName}...";
                    allResults.AddRange(_validationService.RunAllChecks(subject));

                    // Level 2: Sincronizare cu Planul
                    StatusText.Text = $"Sincronizare Level 2 — {subject.SubjectName}...";
                    var planEntry = await _aiService.ExtractFromStudyPlanAsync(planFullPath, subject.SubjectName);
                    allResults.AddRange(_syncService.RunAllChecks(subject, planEntry));

                    // ★ NOU: Adăugăm rezultatele (erorile, avertizările) acestei materii în contextul pentru AI
                    contextBuilder.AppendLine($"\nDISCIPLINA: {subject.SubjectName}");
                    foreach (var res in allResults)
                    {
                        contextBuilder.AppendLine($"- [{res.Severity}] {res.Message}");
                    }

                    // ── PASUL 3: Afișare inteligentă a rezultatelor ──
                    // Dacă nu avem nicio eroare critică, afișăm „Scutul Verde” pentru această materie
                    if (!allResults.Any(r => r.Severity == CodeStormHackathon.Services.ValidationSeverity.Error))
                    {
                        var successContainer = new StackPanel { Margin = new Thickness(10, 0, 10, 10) };
                        successContainer.Children.Add(new PackIcon
                        {
                            Kind = PackIconKind.ShieldCheck,
                            Foreground = Brushes.Green,
                            HorizontalAlignment = HorizontalAlignment.Center
                        });
                        successContainer.Children.Add(new TextBlock
                        {
                            Text = "Disciplina respectă toate normele critice.",
                            Foreground = Brushes.Green,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            FontSize = 11,
                            FontStyle = FontStyles.Italic
                        });
                        ErrorsListBox.Items.Add(successContainer);
                    }

                    // Afișăm detaliile (erori, avertizări, succese)
                    DisplayResults(allResults);
                }

                // ★ NOU: Salvăm raportul complet ca și context pentru chat
                _chatContext = contextBuilder.ToString();

                StatusText.Text = $"✅ Audit complet.";
            }
            catch (Exception ex)
            {
                AddResult(CodeStormHackathon.Services.ValidationResult.Error("SISTEM", $"Eroare: {ex.Message}"));
                StatusText.Text = "❌ Eroare la procesare.";
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
                BtnAiAnalysis.IsEnabled = true;
                LoadingSpinner.Visibility = Visibility.Collapsed;
            }
        }

        // Helper pentru crearea cardului de sumar la începutul listei
        private Border CreateSummaryCard(string text)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(249, 250, 251)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.Indigo });

            border.Child = stack;
            return border;
        }

        // ─────────────────────────────────────────────────────────────────
        // Afișează o listă de ValidationResult în ListBox cu culori
        // ─────────────────────────────────────────────────────────────────
        private void DisplayResults(List<CodeStormHackathon.Services.ValidationResult> results)
        {
            foreach (var result in results)
                AddResult(result);
            ErrorsListBox.ScrollIntoView(ErrorsListBox.Items[ErrorsListBox.Items.Count - 1]);
        }

        private void AddResult(CodeStormHackathon.Services.ValidationResult result)
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
            var border = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(Color.FromRgb(238, 242, 255)), // Indigo foarte deschis
                BorderBrush = new SolidColorBrush(Color.FromRgb(199, 210, 254)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 15, 0, 5),
                CornerRadius = new CornerRadius(4)
            };

            var tb = new System.Windows.Controls.TextBlock
            {
                Text = text.ToUpper(),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(67, 56, 202)),
            };

            border.Child = tb;
            ErrorsListBox.Items.Add(border);
        }

        #region Copilot region
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
            bool isUser = sender == "Tu";

            // Containerul principal pentru mesaj
            var bubble = new System.Windows.Controls.Border
            {
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(isUser ? 50 : 5, 5, isUser ? 5 : 50, 5),
                CornerRadius = isUser ? new CornerRadius(15, 15, 2, 15) : new CornerRadius(15, 15, 15, 2),
                Background = isUser
                    ? new SolidColorBrush(Color.FromRgb(99, 102, 241)) // Indigo pentru utilizator
                    : new SolidColorBrush(Color.FromRgb(243, 244, 246)), // Gri deschis pentru AI
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 5,
                    ShadowDepth = 1,
                    Opacity = 0.1
                }
            };

            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = isUser ? Brushes.White : new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                FontSize = 13
            };

            bubble.Child = textBlock;

            // Adăugăm bula în panoul de chat
            ChatDisplay.Children.Add(bubble);

            // Auto-scroll la ultimul mesaj
            var scrollViewer = ChatDisplay.Parent as System.Windows.Controls.ScrollViewer;
            scrollViewer?.ScrollToBottom();
        }

        #endregion

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

        #region Drag&Drop and Browse
        private void FileDragOver(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void FileDropPlan(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files?.Length > 0)
                {
                    TxtPlanPath.Tag = files[0];
                    TxtPlanPath.Text = System.IO.Path.GetFileName(files[0]);
                }
            }
        }

        private void FileDropFD(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files?.Length > 0)
                {
                    TxtFDPath.Tag = files[0];
                    TxtFDPath.Text = System.IO.Path.GetFileName(files[0]);
                }
            }
        }

        private void BtnBrowsePlan_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
                Title = "Selectează Planul de Învățământ"
            };

            if (dlg.ShowDialog() == true)
            {
                TxtPlanPath.Tag = dlg.FileName;
                TxtPlanPath.Text = System.IO.Path.GetFileName(dlg.FileName);
            }
        }

        private void BtnBrowseFD_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Academic Documents (*.pdf;*.docx)|*.pdf;*.docx|Images (*.jpg;*.png)|*.jpg;*.png",
                Title = "Selectează Fișa Disciplinei"
            };

            if (dlg.ShowDialog() == true)
            {
                TxtFDPath.Tag = dlg.FileName;
                TxtFDPath.Text = System.IO.Path.GetFileName(dlg.FileName);
            }
        }

        #endregion

        private void BtnOpenDiff_Click(object sender, RoutedEventArgs e)
        {
            var diffWindow = new DiffWindow();

            // UX Bonus: Dacă utilizatorul a încărcat deja o Fișă a Disciplinei 
            // în fereastra principală, o precompletăm automat ca fiind "Versiunea Nouă"
            // pentru a-i salva un click. El trebuie doar să caute Versiunea Veche.
            if (TxtFDPath.Tag != null)
            {
                diffWindow.TxtNewPath.Text = TxtFDPath.Tag.ToString();
            }

            diffWindow.Show();
        }

        // ─────────────────────────────────────────────────────────────────
        // HELPERS VIZUALI (Reparați pentru C#)
        // ─────────────────────────────────────────────────────────────────

        private void ShowEmptyState(string message, PackIconKind iconKind, Brush color)
        {
            ErrorsListBox.Items.Clear();

            // StackPanel trebuie să vină din System.Windows.Controls
            var container = new System.Windows.Controls.StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            };

            // PackIcon nu are nevoie de prefixul materialDesign: în C#
            container.Children.Add(new PackIcon
            {
                Kind = iconKind,
                Width = 80,
                Height = 80,
                Foreground = color,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            container.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = message,
                Foreground = Brushes.Gray,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 15, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });

            ErrorsListBox.Items.Add(container);
        }

        private void RunBusinessValidations(SyllabusData data)
        {
            // Apelăm serviciul tău de validare existent
            var errors = _validationService.RunAllChecks(data);

            if (errors == null || errors.Count == 0 || !errors.Any(x => x.Severity == ValidationSeverity.Error))
            {
                // PackIconKind.ShieldCheck este enumerarea corectă
                ShowEmptyState("Documentul este perfect conform!\nNu au fost detectate erori critice.",
                               PackIconKind.ShieldCheck,
                               new SolidColorBrush(Color.FromRgb(16, 185, 129))); // Verde smarald
            }

            // Indiferent dacă avem succes sau nu, afișăm rezultatele detaliate sub empty state
            DisplayResults(errors);
        }

        private void ShowChatEmptyState()
        {
            ChatDisplay.Children.Clear();
            var welcome = new System.Windows.Controls.TextBlock
            {
                Text = "👋 Bună! Sunt asistentul tău academic.\nÎncarcă documentele și pune-mi orice întrebare despre conformitatea lor.",
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(20, 100, 20, 0),
                TextWrapping = TextWrapping.Wrap
            };
            ChatDisplay.Children.Add(welcome);
        }

        private void BtnOpenBulk_Click(object sender, RoutedEventArgs e)
        {
            new BulkUpdateWindow().Show();
        }
    }
}