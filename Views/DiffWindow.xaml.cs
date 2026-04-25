using CodeStormHackathon.Models;
using CodeStormHackathon.Services;
using SistemAcademic.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodeStormHackathon.Views
{
    public partial class DiffWindow : Window
    {
        private readonly AIService _aiService = new AIService();
        private readonly SyllabusDiffService _diffService = new SyllabusDiffService();

        // Dacă se deschide fereastra cu date deja extrase din MainWindow,
        // le folosim direct fără a mai apela Gemini
        private SyllabusData _preloadedOld;
        private SyllabusData _preloadedNew;

        public DiffWindow()
        {
            InitializeComponent();
        }

        // Constructor alternativ: primește datele deja extrase
        public DiffWindow(SyllabusData oldData, SyllabusData newData) : this()
        {
            _preloadedOld = oldData;
            _preloadedNew = newData;
            TxtOldPath.Text = $"[Date preîncărcate: {oldData?.SubjectName}]";
            TxtNewPath.Text = $"[Date preîncărcate: {newData?.SubjectName}]";
        }

        // ─────────────────────────────────────────────────────────────────
        // Buton Analizează Diferențele
        // ─────────────────────────────────────────────────────────────────
        private async void BtnRunDiff_Click(object sender, RoutedEventArgs e)
        {
            OldPanel.Children.Clear();
            NewPanel.Children.Clear();
            SummaryBorder.Visibility = Visibility.Collapsed;

            BtnRunDiff.IsEnabled = false;
            DiffSpinner.Visibility = Visibility.Visible;
            StatusInfoText.Text = "Se extrag datele...";

            try
            {
                SyllabusData oldFd;
                SyllabusData newFd;

                if (_preloadedOld != null && _preloadedNew != null)
                {
                    // Folosim datele preîncărcate din MainWindow
                    oldFd = _preloadedOld;
                    newFd = _preloadedNew;
                }
                else
                {
                    // Trebuie să extragă din fișiere via AI
                    if (string.IsNullOrEmpty(TxtOldPath.Text) || string.IsNullOrEmpty(TxtNewPath.Text))
                    {
                        MessageBox.Show("Te rog selectează ambele fișiere pentru comparație.",
                            "Fișiere lipsă", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    StatusInfoText.Text = "Extrag FD Veche...";
                    var oldList = await _aiService.ExtractAllSyllabusDataAsync(TxtOldPath.Text);
                    oldFd = oldList?.Count > 0 ? oldList[0] : null;

                    StatusInfoText.Text = "Extrag FD Nouă...";
                    var newList = await _aiService.ExtractAllSyllabusDataAsync(TxtNewPath.Text);
                    newFd = newList?.Count > 0 ? newList[0] : null;
                }

                if (oldFd == null || newFd == null)
                {
                    StatusInfoText.Text = "❌ Nu s-au putut extrage datele din documente.";
                    return;
                }

                StatusInfoText.Text = "Se calculează diferențele...";
                var report = _diffService.Compare(oldFd, newFd);

                RenderDiff(report);

                // Sumar
                StatusInfoText.Text = "AI-ul genereaza explicatia narativa...";
                string narrativeReport = await _aiService.GetNarrativeDeltaReportAsync(oldFd, newFd);
                SummaryText.Text = narrativeReport;
                SummaryBorder.Visibility = Visibility.Visible;
                if (report.HasChanges)
                    SummaryText.Text = $"⚡ {report.TotalChanges} modificări detectate între cele două versiuni.";
                else
                    SummaryText.Text = "✅ Documentele sunt identice — nicio diferență detectată.";

                StatusInfoText.Text = report.HasChanges
                    ? $"Diff complet — {report.TotalChanges} modificări."
                    : "Diff complet — documente identice.";
            }
            catch (Exception ex)
            {
                StatusInfoText.Text = $"❌ Eroare: {ex.Message}";
            }
            finally
            {
                BtnRunDiff.IsEnabled = true;
                DiffSpinner.Visibility = Visibility.Collapsed;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Randează raportul de diff în cele 2 paneluri
        // ─────────────────────────────────────────────────────────────────
        private void RenderDiff(SyllabusDiffReport report)
        {
            // ── Câmpuri scalare ──
            AddSectionHeader(OldPanel, "📋 Câmpuri Principale");
            AddSectionHeader(NewPanel, "📋 Câmpuri Principale");

            foreach (var field in report.FieldDiffs)
            {
                AddFieldRow(OldPanel, field.FieldName, field.OldValue, field.Status, isOldSide: true);
                AddFieldRow(NewPanel, field.FieldName, field.NewValue, field.Status, isOldSide: false);
            }

            // ── Capitole curs ──
            AddSectionHeader(OldPanel, "📖 Tematică Curs (Capitole)");
            AddSectionHeader(NewPanel, "📖 Tematică Curs (Capitole)");

            foreach (var item in report.ChapterDiffs)
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

            if (report.ChapterDiffs.Count == 0)
            {
                AddPlaceholder(OldPanel, "— Nicio temă extrasă —");
                AddPlaceholder(NewPanel, "— Nicio temă extrasă —");
            }

            // ── Competențe ──
            AddSectionHeader(OldPanel, "🎯 Competențe (CP / CT)");
            AddSectionHeader(NewPanel, "🎯 Competențe (CP / CT)");

            foreach (var item in report.CompetencyDiffs)
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

        private void AddFieldRow(StackPanel panel, string label, string value,
            DiffStatus status, bool isOldSide)
        {
            // Determină culoarea în funcție de status și ce parte afișăm
            var (bg, fg, prefix) = GetDiffColors(status, isOldSide);

            var border = new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 2, 0, 2)
            };

            var inner = new StackPanel();
            inner.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                FontWeight = FontWeights.SemiBold
            });
            inner.Children.Add(new TextBlock
            {
                Text = prefix + value,
                Foreground = fg,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13
            });

            border.Child = inner;
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

        // Returnează (background, foreground, prefix emoji) în funcție de status
        private (Brush bg, Brush fg, string prefix) GetDiffColors(
            DiffStatus status, bool isOldSide)
        {
            return status switch
            {
                DiffStatus.Removed =>
                    (new SolidColorBrush(Color.FromRgb(254, 226, 226)),  // roșu deschis
                     new SolidColorBrush(Color.FromRgb(185, 28, 28)),    // roșu închis
                     "🔴 "),
                DiffStatus.Added =>
                    (new SolidColorBrush(Color.FromRgb(220, 252, 231)),  // verde deschis
                     new SolidColorBrush(Color.FromRgb(21, 128, 61)),    // verde închis
                     "🟢 "),
                DiffStatus.Modified when isOldSide =>
                    (new SolidColorBrush(Color.FromRgb(254, 226, 226)),
                     new SolidColorBrush(Color.FromRgb(185, 28, 28)),
                     "🔴 "),
                DiffStatus.Modified when !isOldSide =>
                    (new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                     new SolidColorBrush(Color.FromRgb(21, 128, 61)),
                     "🟢 "),
                _ =>    // Unchanged
                    (new SolidColorBrush(Color.FromRgb(248, 250, 252)),  // gri foarte deschis
                     new SolidColorBrush(Color.FromRgb(51, 65, 85)),     // gri închis
                     "   ")
            };
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

        private void FileDragOver(object s, DragEventArgs e) =>
            e.Effects = DragDropEffects.Copy;

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