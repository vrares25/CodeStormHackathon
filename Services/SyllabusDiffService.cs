using CodeStormHackathon.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeStormHackathon.Services
{
    // ─────────────────────────────────────────────────────────────────
    // Modelele de rezultat pentru Diff
    // ─────────────────────────────────────────────────────────────────
    public enum DiffStatus
    {
        Unchanged,  // identic în ambele versiuni
        Modified,   // există în ambele dar e diferit
        Added,      // există doar în versiunea nouă
        Removed     // există doar în versiunea veche
    }

    public class FieldDiff
    {
        public string FieldName { get; set; }       // ex: "Credite", "Tip Evaluare"
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public DiffStatus Status { get; set; }
    }

    public class ListItemDiff
    {
        public string Content { get; set; }
        public DiffStatus Status { get; set; }
    }

    public class SyllabusDiffReport
    {
        public string OldSubjectName { get; set; }
        public string NewSubjectName { get; set; }

        // Câmpuri scalare (credite, evaluare, ponderi, bibliografie)
        public List<FieldDiff> FieldDiffs { get; set; } = new List<FieldDiff>();

        // Liste (capitole, competențe)
        public List<ListItemDiff> ChapterDiffs { get; set; } = new List<ListItemDiff>();
        public List<ListItemDiff> CompetencyDiffs { get; set; } = new List<ListItemDiff>();

        // Sumar rapid
        public int TotalChanges => FieldDiffs.Count(f => f.Status != DiffStatus.Unchanged)
                                 + ChapterDiffs.Count(c => c.Status != DiffStatus.Unchanged)
                                 + CompetencyDiffs.Count(c => c.Status != DiffStatus.Unchanged);

        public bool HasChanges => TotalChanges > 0;
    }

    // ─────────────────────────────────────────────────────────────────
    // Serviciul de Diff — pur C#, fără AI
    // ─────────────────────────────────────────────────────────────────
    public class SyllabusDiffService
    {
        public SyllabusDiffReport Compare(SyllabusData oldFd, SyllabusData newFd)
        {
            var report = new SyllabusDiffReport
            {
                OldSubjectName = oldFd?.SubjectName ?? "Nedefinit",
                NewSubjectName = newFd?.SubjectName ?? "Nedefinit"
            };

            // ── Câmpuri scalare ──
            report.FieldDiffs.Add(CompareField(
                "Denumire Disciplină",
                oldFd?.SubjectName, newFd?.SubjectName));

            report.FieldDiffs.Add(CompareField(
                "Credite",
                oldFd?.Credits.ToString(), newFd?.Credits.ToString()));

            report.FieldDiffs.Add(CompareField(
                "Tip Evaluare",
                oldFd?.EvaluationType, newFd?.EvaluationType));

            report.FieldDiffs.Add(CompareField(
                "Pondere Examen Final",
                oldFd?.FinalExamWeight > 0 ? $"{oldFd.FinalExamWeight}%" : "—",
                newFd?.FinalExamWeight > 0 ? $"{newFd.FinalExamWeight}%" : "—"));

            report.FieldDiffs.Add(CompareField(
                "Pondere Activitate",
                oldFd?.ActivityWeight > 0 ? $"{oldFd.ActivityWeight}%" : "—",
                newFd?.ActivityWeight > 0 ? $"{newFd.ActivityWeight}%" : "—"));

            report.FieldDiffs.Add(CompareField(
                "Bibliografie",
                NormalizeBiblio(oldFd?.Bibliography),
                NormalizeBiblio(newFd?.Bibliography)));

            // ── Liste: Capitole curs ──
            report.ChapterDiffs = DiffList(
                oldFd?.CourseChapters ?? new List<string>(),
                newFd?.CourseChapters ?? new List<string>());

            // ── Liste: Competențe ──
            report.CompetencyDiffs = DiffList(
                oldFd?.Competencies ?? new List<string>(),
                newFd?.Competencies ?? new List<string>());

            return report;
        }

        // ─────────────────────────────────────────────────────────────────
        // Compară un câmp scalar
        // ─────────────────────────────────────────────────────────────────
        private FieldDiff CompareField(string fieldName, string oldVal, string newVal)
        {
            oldVal = string.IsNullOrWhiteSpace(oldVal) ? "—" : oldVal.Trim();
            newVal = string.IsNullOrWhiteSpace(newVal) ? "—" : newVal.Trim();

            DiffStatus status;
            if (oldVal == "—" && newVal != "—")
                status = DiffStatus.Added;
            else if (oldVal != "—" && newVal == "—")
                status = DiffStatus.Removed;
            else if (NormalizeForCompare(oldVal) == NormalizeForCompare(newVal))
                status = DiffStatus.Unchanged;
            else
                status = DiffStatus.Modified;

            return new FieldDiff
            {
                FieldName = fieldName,
                OldValue = oldVal,
                NewValue = newVal,
                Status = status
            };
        }

        // ─────────────────────────────────────────────────────────────────
        // Diff pe liste (LCS simplificat):
        // Marchează fiecare item ca Added / Removed / Unchanged
        // ─────────────────────────────────────────────────────────────────
        private List<ListItemDiff> DiffList(List<string> oldList, List<string> newList)
        {
            var result = new List<ListItemDiff>();

            // Normalizăm pentru comparație
            var oldNorm = oldList.Select(NormalizeForCompare).ToList();
            var newNorm = newList.Select(NormalizeForCompare).ToList();

            // Items din OLD
            foreach (var (item, idx) in oldList.Select((v, i) => (v, i)))
            {
                string norm = oldNorm[idx];
                if (newNorm.Contains(norm))
                    result.Add(new ListItemDiff { Content = item, Status = DiffStatus.Unchanged });
                else
                    result.Add(new ListItemDiff { Content = item, Status = DiffStatus.Removed });
            }

            // Items din NEW care nu sunt în OLD
            foreach (var (item, idx) in newList.Select((v, i) => (v, i)))
            {
                string norm = newNorm[idx];
                if (!oldNorm.Contains(norm))
                    result.Add(new ListItemDiff { Content = item, Status = DiffStatus.Added });
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────

        // Normalizează pentru comparație: lowercase, fără diacritice, fără spații extra
        private string NormalizeForCompare(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            return s.ToLower()
                .Replace("ș", "s").Replace("ț", "t")
                .Replace("ă", "a").Replace("â", "a").Replace("î", "i")
                .Replace("  ", " ").Trim();
        }

        // Normalizează bibliografia pentru comparație (ignoră whitespace excesiv)
        private string NormalizeBiblio(string b)
        {
            if (string.IsNullOrWhiteSpace(b)) return "—";
            // Dacă e lungă, afișăm doar primele 100 de caractere în câmpul scalar
            return b.Trim().Length > 100
                ? b.Trim().Substring(0, 100) + "..."
                : b.Trim();
        }
    }
}