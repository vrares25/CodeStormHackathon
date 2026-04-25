using CodeStormHackathon.Models;
using CodeStormHackathon.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodeStormHackathon.Views
{
    public partial class DiffWindow : Window
    {
        private readonly AIService _aiService = new AIService();
        private readonly SyllabusDiffService _diffService = new SyllabusDiffService();

        private SyllabusData _preloadedOld;
        private SyllabusData _preloadedNew;
        private SyllabusDiffReport _currentReport;

        public DiffWindow()
        {
            InitializeComponent();

            // Logica de Sync Scroll
            OldScroll.ScrollChanged += (s, e) => {
                NewScroll.ScrollToVerticalOffset(OldScroll.VerticalOffset);
            };
            NewScroll.ScrollChanged += (s, e) => {
                OldScroll.ScrollToVerticalOffset(NewScroll.VerticalOffset);
            };

            this.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) this.Close(); };
        }

        public DiffWindow(SyllabusData oldData, SyllabusData newData) : this()
        {
            _preloadedOld = oldData;
            _preloadedNew = newData;
            TxtOldPath.Text = $"[Date preîncărcate: {oldData?.SubjectName}]";
            TxtNewPath.Text = $"[Date preîncărcate: {newData?.SubjectName}]";
        }

        private void ChkOnlyChanges_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_currentReport != null)
            {
                RenderDiff(_currentReport);
            }
        }

        private async void BtnRunDiff_Click(object sender, RoutedEventArgs e)
        {
            OldPanel.Children.Clear();
            NewPanel.Children.Clear();
            SummaryBorder.Visibility = Visibility.Collapsed;

            BtnRunDiff.IsEnabled = false;
            DiffSpinner.Visibility = Visibility.Visible;
            StatusInfoText.Text = "Se procesează documentele...";
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            try
            {
                SyllabusData oldFd;
                SyllabusData newFd;

                if (_preloadedOld != null && _preloadedNew != null)
                {
                    oldFd = _preloadedOld;
                    newFd = _preloadedNew;
                }
                else
                {
                    if (string.IsNullOrEmpty(TxtOldPath.Text) || string.IsNullOrEmpty(TxtNewPath.Text))
                    {
                        MessageBox.Show("Te rog selectează ambele fișiere pentru comparație.",
                            "Fișiere lipsă", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    StatusInfoText.Text = "Extrag datele prin AI...";
                    var oldList = await _aiService.ExtractAllSyllabusDataAsync(TxtOldPath.Text);
                    oldFd = oldList?.Count > 0 ? oldList[0] : null;

                    var newList = await _aiService.ExtractAllSyllabusDataAsync(TxtNewPath.Text);
                    newFd = newList?.Count > 0 ? newList[0] : null;
                }

                if (oldFd == null || newFd == null)
                {
                    StatusInfoText.Text = "❌ Nu s-au putut extrage datele.";
                    return;
                }

                StatusInfoText.Text = "Se calculează diferențele...";
                var report = _diffService.Compare(oldFd, newFd);

                _currentReport = report; // Salvăm raportul pentru filtru
                RenderDiff(report);

                StatusInfoText.Text = "AI-ul generează explicația narativă...";
                string narrativeReport = await _aiService.GetNarrativeDeltaReportAsync(oldFd, newFd);

                TxtDiffStats.Text = report.HasChanges
                    ? $"⚡ {report.TotalChanges} MODIFICĂRI DETECTATE"
                    : "✅ DOCUMENTE IDENTICE";

                SummaryText.Text = narrativeReport;
                SummaryBorder.Visibility = Visibility.Visible;

                StatusInfoText.Text = report.HasChanges
                    ? $"Diff complet — {report.TotalChanges} modificări."
                    : "Diff complet — nicio modificare.";
            }
            catch (Exception ex)
            {
                StatusInfoText.Text = $"❌ Eroare: {ex.Message}";
                MessageBox.Show($"Eroare la procesarea diff-ului: {ex.Message}", "Eroare", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnRunDiff.IsEnabled = true;
                DiffSpinner.Visibility = Visibility.Collapsed;
                System.Windows.Input.Mouse.OverrideCursor = null;
            }
        }

        private void RenderDiff(SyllabusDiffReport report)
        {
            OldPanel.Children.Clear();
            NewPanel.Children.Clear();

            bool showOnlyChanges = ChkOnlyChanges.IsChecked == true;

            // ────────────────────────────────────────────────────────
            // 1. CÂMPURI PRINCIPALE
            // ────────────────────────────────────────────────────────
            AddSectionHeader(OldPanel, "📋 Câmpuri Principale");
            AddSectionHeader(NewPanel, "📋 Câmpuri Principale");

            var visibleFields = showOnlyChanges
                ? report.FieldDiffs.Where(f => f.Status != DiffStatus.Unchanged).ToList()
                : report.FieldDiffs;

            if (report.FieldDiffs.Count > 0 && visibleFields.Count == 0)
            {
                AddSuccessState(OldPanel, "Nicio modificare la datele generale.");
                AddSuccessState(NewPanel, "Nicio modificare la datele generale.");
            }
            else
            {
                foreach (var field in visibleFields)
                {
                    AddFieldRow(OldPanel, field.FieldName, field.OldValue, field.NewValue, field.Status, isOldSide: true);
                    AddFieldRow(NewPanel, field.FieldName, field.OldValue, field.NewValue, field.Status, isOldSide: false);
                }
            }

            // ────────────────────────────────────────────────────────
            // 2. CAPITOLE (TEMATICĂ)
            // ────────────────────────────────────────────────────────
            AddSectionHeader(OldPanel, "📖 Tematică Curs (Capitole)");
            AddSectionHeader(NewPanel, "📖 Tematică Curs (Capitole)");

            var visibleChapters = showOnlyChanges
                ? report.ChapterDiffs.Where(c => c.Status != DiffStatus.Unchanged).ToList()
                : report.ChapterDiffs;

            if (report.ChapterDiffs.Count > 0 && visibleChapters.Count == 0)
            {
                AddSuccessState(OldPanel, "Structura cursului este identică.");
                AddSuccessState(NewPanel, "Structura cursului este identică.");
            }
            else
            {
                foreach (var item in visibleChapters)
                {
                    if (item.Status == DiffStatus.Removed)
                    {
                        AddListItem(OldPanel, item.Content, DiffStatus.Removed);
                        AddPlaceholder(NewPanel, "(capitol eliminat)");
                    }
                    else if (item.Status == DiffStatus.Added)
                    {
                        AddPlaceholder(OldPanel, "(capitol nou)");
                        AddListItem(NewPanel, item.Content, DiffStatus.Added);
                    }
                    else
                    {
                        AddListItem(OldPanel, item.Content, DiffStatus.Unchanged);
                        AddListItem(NewPanel, item.Content, DiffStatus.Unchanged);
                    }
                }
            }

            if (report.ChapterDiffs.Count == 0)
            {
                AddPlaceholder(OldPanel, "— Nicio temă extrasă —");
                AddPlaceholder(NewPanel, "— Nicio temă extrasă —");
            }

            // ────────────────────────────────────────────────────────
            // 3. COMPETENȚE
            // ────────────────────────────────────────────────────────
            AddSectionHeader(OldPanel, "🎯 Competențe (CP / CT)");
            AddSectionHeader(NewPanel, "🎯 Competențe (CP / CT)");

            var visibleComps = showOnlyChanges
                ? report.CompetencyDiffs.Where(c => c.Status != DiffStatus.Unchanged).ToList()
                : report.CompetencyDiffs;

            if (report.CompetencyDiffs.Count > 0 && visibleComps.Count == 0)
            {
                AddSuccessState(OldPanel, "Competențele au rămas neschimbate.");
                AddSuccessState(NewPanel, "Competențele au rămas neschimbate.");
            }
            else
            {
                foreach (var item in visibleComps)
                {
                    if (item.Status == DiffStatus.Removed)
                    {
                        AddListItem(OldPanel, item.Content, DiffStatus.Removed);
                        AddPlaceholder(NewPanel, "(competență eliminată)");
                    }
                    else if (item.Status == DiffStatus.Added)
                    {
                        AddPlaceholder(OldPanel, "(competență nouă)");
                        AddListItem(NewPanel, item.Content, DiffStatus.Added);
                    }
                    else
                    {
                        AddListItem(OldPanel, item.Content, DiffStatus.Unchanged);
                        AddListItem(NewPanel, item.Content, DiffStatus.Unchanged);
                    }
                }
            }

            if (report.CompetencyDiffs.Count == 0)
            {
                AddPlaceholder(OldPanel, "— Nicio competență extrasă —");
                AddPlaceholder(NewPanel, "— Nicio competență extrasă —");
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // HELPERS UI
        // ─────────────────────────────────────────────────────────────────
        private void AddSectionHeader(StackPanel panel, string title)
        {
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                Margin = new Thickness(0, 14, 0, 4)
            });

            panel.Children.Add(new Separator
            {
                Background = new SolidColorBrush(Color.FromRgb(199, 210, 254)),
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        private void AddFieldRow(StackPanel panel, string label, string oldValue, string newValue, DiffStatus status, bool isOldSide)
        {
            var (bg, fg, prefix) = GetDiffColors(status, isOldSide);
            string currentValue = isOldSide ? oldValue : newValue;

            var border = new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 3, 0, 3),
                BorderThickness = new Thickness(0)
            };

            var innerStack = new StackPanel();
            innerStack.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), FontWeight = FontWeights.SemiBold });

            var valueContent = new System.Windows.Controls.TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = fg,
                FontSize = 13
            };

            SetSmartHighlighting(valueContent, oldValue, newValue, status, isOldSide);

            innerStack.Children.Add(valueContent);
            border.Child = innerStack;
            panel.Children.Add(border);
        }

        private void AddListItem(StackPanel panel, string content, DiffStatus status)
        {
            bool isOld = (status == DiffStatus.Removed);
            var (bg, fg, prefix) = GetDiffColors(status, isOld);

            panel.Children.Add(new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 2, 0, 2),
                Child = new TextBlock
                {
                    Text = prefix + content,
                    Foreground = fg,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12
                }
            });
        }

        private void AddPlaceholder(StackPanel panel, string text)
        {
            panel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 2, 0, 2),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    FontStyle = FontStyles.Italic,
                    FontSize = 12
                }
            });
        }

        private void AddSuccessState(StackPanel panel, string message)
        {
            panel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 15, 10, 15),
                Margin = new Thickness(0, 5, 0, 15),
                Child = new TextBlock
                {
                    Text = "✅ " + message,
                    Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61)),
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            });
        }

        private (Brush bg, Brush fg, string prefix) GetDiffColors(DiffStatus status, bool isOldSide)
        {
            return status switch
            {
                DiffStatus.Removed =>
                    (new SolidColorBrush(Color.FromRgb(254, 226, 226)),
                     new SolidColorBrush(Color.FromRgb(185, 28, 28)),
                     "🔴 "),
                DiffStatus.Added =>
                    (new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                     new SolidColorBrush(Color.FromRgb(21, 128, 61)),
                     "🟢 "),
                DiffStatus.Modified when isOldSide =>
                    (new SolidColorBrush(Color.FromRgb(254, 226, 226)),
                     new SolidColorBrush(Color.FromRgb(185, 28, 28)),
                     "🔴 "),
                DiffStatus.Modified when !isOldSide =>
                    (new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                     new SolidColorBrush(Color.FromRgb(21, 128, 61)),
                     "🟢 "),
                _ =>
                    (new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                     new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                     "   ")
            };
        }

        private void SetSmartHighlighting(System.Windows.Controls.TextBlock textBlock, string oldText, string newText, DiffStatus status, bool isOldSide)
        {
            textBlock.Inlines.Clear();

            // Dacă nu avem modificări sau lipsesc texte, afișăm doar valoarea corectă pentru partea respectivă
            if (status == DiffStatus.Unchanged || string.IsNullOrEmpty(oldText) || string.IsNullOrEmpty(newText))
            {
                textBlock.Text = isOldSide ? (oldText ?? "") : (newText ?? "");
                return;
            }

            var oldWords = oldText.Split(' ');
            var newWords = newText.Split(' ');

            if (isOldSide)
            {
                // ── PARTEA STÂNGĂ (VECHE) ──
                // Afișăm textul vechi. Dacă un cuvânt nu mai există în textul nou, îl marcăm ca "șters" (roșu)
                foreach (var word in oldWords)
                {
                    bool isRemoved = !newWords.Contains(word);
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(word + " ")
                    {
                        Background = isRemoved ? new SolidColorBrush(Color.FromArgb(80, 239, 68, 68)) : Brushes.Transparent, // Fundal roșiatic
                        FontWeight = isRemoved ? FontWeights.Bold : FontWeights.Normal,
                        TextDecorations = isRemoved ? TextDecorations.Strikethrough : null // Taie textul
                    });
                }
            }
            else
            {
                // ── PARTEA DREAPTĂ (NOUĂ) ──
                // Afișăm textul nou. Dacă un cuvânt nu exista în textul vechi, îl marcăm ca "nou" (verde)
                foreach (var word in newWords)
                {
                    bool isNew = !oldWords.Contains(word);
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(word + " ")
                    {
                        Background = isNew ? new SolidColorBrush(Color.FromArgb(80, 34, 197, 94)) : Brushes.Transparent, // Fundal verde
                        FontWeight = isNew ? FontWeights.Bold : FontWeights.Normal
                    });
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Drag & Drop + Browse
        // ─────────────────────────────────────────────────────────────────
        private void BtnBrowseOld_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Academic Documents (*.pdf;*.docx)|*.pdf;*.docx",
                Title = "Selectează FD Versiune Veche"
            };
            if (dlg.ShowDialog() == true) TxtOldPath.Text = dlg.FileName;
        }

        private void BtnBrowseNew_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Academic Documents (*.pdf;*.docx)|*.pdf;*.docx",
                Title = "Selectează FD Versiune Nouă"
            };
            if (dlg.ShowDialog() == true) TxtNewPath.Text = dlg.FileName;
        }

        private void FileDragOver(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void FileDropOld(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                TxtOldPath.Text = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
        }

        private void FileDropNew(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                TxtNewPath.Text = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
        }
    }
}