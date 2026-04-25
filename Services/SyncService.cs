using CodeStormHackathon.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeStormHackathon.Services
{
    public class SyncService
    {
        // ─────────────────────────────────────────────────────────────────
        // UC 2.1 — Sync Master: compară FD cu Planul de Învățământ
        // Verifică: denumire, credite, tip evaluare
        // ─────────────────────────────────────────────────────────────────
        public List<ValidationResult> CompareWithPlan(SyllabusData fd, StudyPlanEntry planEntry)
        {
            var results = new List<ValidationResult>();

            // Verifică dacă materia a fost găsită în Plan
            if (planEntry == null || planEntry.Credits == 0)
            {
                results.Add(ValidationResult.Error(
                    "UC2.1", $"[{fd.SubjectName}] Materia NU a fost găsită în Planul de Învățământ."));
                return results;
            }

            bool allOk = true;

            // Verificare credite
            if (fd.Credits != planEntry.Credits)
            {
                results.Add(ValidationResult.Error(
                    "UC2.1", $"[{fd.SubjectName}] Conflict credite: FD are {fd.Credits}, Planul are {planEntry.Credits}."));
                allOk = false;
            }
            else
            {
                results.Add(ValidationResult.Success(
                    "UC2.1", $"[{fd.SubjectName}] Credite OK: {fd.Credits} credite concordă cu Planul."));
            }

            // Verificare tip evaluare (normalizat pentru comparație sigură)
            string fdEval = NormalizeEvalType(fd.EvaluationType);
            string planEval = NormalizeEvalType(planEntry.EvaluationType);

            if (fdEval != planEval)
            {
                results.Add(ValidationResult.Error(
                    "UC2.1", $"[{fd.SubjectName}] Conflict evaluare: FD zice \"{fd.EvaluationType}\", Planul zice \"{planEntry.EvaluationType}\"."));
                allOk = false;
            }
            else
            {
                results.Add(ValidationResult.Success(
                    "UC2.1", $"[{fd.SubjectName}] Tip evaluare OK: \"{fd.EvaluationType}\" concordă cu Planul."));
            }

            // Verificare denumire materie (fuzzy — avertizare, nu eroare)
            if (!string.IsNullOrWhiteSpace(planEntry.SubjectName))
            {
                bool nameMatch = NormalizeName(fd.SubjectName)
                    .Contains(NormalizeName(planEntry.SubjectName)) ||
                    NormalizeName(planEntry.SubjectName)
                    .Contains(NormalizeName(fd.SubjectName));

                if (!nameMatch)
                    results.Add(ValidationResult.Warning(
                        "UC2.1", $"[{fd.SubjectName}] Denumirea diferă față de Plan: \"{planEntry.SubjectName}\". Verificați ortografia."));
            }

            return results;
        }

        // ─────────────────────────────────────────────────────────────────
        // UC 2.2 — Competency Mapper: verifică dacă competențele din FD
        //          există în lista oficială din Planul de Învățământ
        // ─────────────────────────────────────────────────────────────────
        public List<ValidationResult> MapCompetencies(SyllabusData fd, StudyPlanEntry planEntry)
        {
            var results = new List<ValidationResult>();

            bool fdHasCompetencies = fd.Competencies != null && fd.Competencies.Count > 0;
            bool planHasCompetencies = planEntry.Competencies != null && planEntry.Competencies.Count > 0;

            // Dacă FD nu are competențe deloc
            if (!fdHasCompetencies)
            {
                results.Add(ValidationResult.Error(
                    "UC2.2", $"[{fd.SubjectName}] FD nu conține nicio competență (CP/CT) declarată."));
                return results;
            }

            // Dacă Planul nu are competențe (posibil neextrase)
            if (!planHasCompetencies)
            {
                results.Add(ValidationResult.Warning(
                    "UC2.2", $"[{fd.SubjectName}] Planul de Învățământ nu conține competențe pentru această materie — comparația nu poate fi realizată."));

                // Raportăm totuși competențele găsite în FD
                results.Add(ValidationResult.Success(
                    "UC2.2", $"[{fd.SubjectName}] Competențe declarate în FD ({fd.Competencies.Count}): " +
                             string.Join("; ", fd.Competencies.Take(3)) +
                             (fd.Competencies.Count > 3 ? "..." : "")));
                return results;
            }

            // Comparație fuzzy: pentru fiecare competență din FD, caută un match în Plan
            var matched = new List<string>();
            var unmatched = new List<string>();

            foreach (var fdComp in fd.Competencies)
            {
                bool found = planEntry.Competencies.Any(planComp =>
                    FuzzyMatch(fdComp, planComp));

                if (found)
                    matched.Add(fdComp);
                else
                    unmatched.Add(fdComp);
            }

            // Raport match-uri
            if (matched.Count > 0)
                results.Add(ValidationResult.Success(
                    "UC2.2", $"[{fd.SubjectName}] {matched.Count}/{fd.Competencies.Count} competențe verificate și găsite în Plan."));

            // Raport competențe negăsite în Plan
            foreach (var comp in unmatched)
                results.Add(ValidationResult.Warning(
                    "UC2.2", $"[{fd.SubjectName}] Competența din FD nu a fost găsită în Plan: \"{TruncateString(comp, 80)}\""));

            // Verifică dacă există competențe în Plan care lipsesc din FD
            var missingFromFd = planEntry.Competencies
                .Where(planComp => !fd.Competencies.Any(fdComp => FuzzyMatch(fdComp, planComp)))
                .ToList();

            foreach (var comp in missingFromFd)
                results.Add(ValidationResult.Warning(
                    "UC2.2", $"[{fd.SubjectName}] Competența din Plan lipsește din FD: \"{TruncateString(comp, 80)}\""));

            return results;
        }

        // ─────────────────────────────────────────────────────────────────
        // Metodă combinată: rulează toate verificările Level 2
        // ─────────────────────────────────────────────────────────────────
        public List<ValidationResult> RunAllChecks(SyllabusData fd, StudyPlanEntry planEntry)
        {
            var all = new List<ValidationResult>();
            all.AddRange(CompareWithPlan(fd, planEntry));
            all.AddRange(MapCompetencies(fd, planEntry));
            return all;
        }

        // ─────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────

        // Normalizează tipul de evaluare pentru comparație (ignoră diacritice și majuscule)
        private string NormalizeEvalType(string evalType)
        {
            if (string.IsNullOrWhiteSpace(evalType)) return "";
            return evalType.ToLower()
                .Replace("ș", "s").Replace("ț", "t")
                .Replace("ă", "a").Replace("â", "a").Replace("î", "i")
                .Trim();
        }

        // Normalizează denumirile pentru comparație fuzzy
        private string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            return name.ToLower()
                .Replace("ș", "s").Replace("ț", "t")
                .Replace("ă", "a").Replace("â", "a").Replace("î", "i")
                .Trim();
        }

        // Match fuzzy simplu între 2 competențe:
        // consideră match dacă codul (ex: CP1, CT2) apare în ambele
        // sau dacă cel puțin 40% din cuvintele cheie coincid
        private bool FuzzyMatch(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;

            string normA = NormalizeName(a);
            string normB = NormalizeName(b);

            // Match exact
            if (normA == normB) return true;

            // Match pe codul competenței (CP1, CT2, etc.)
            string[] prefixes = { "cp1", "cp2", "cp3", "cp4", "cp5", "cp6",
                                  "ct1", "ct2", "ct3", "ct4", "ct5", "ct6" };
            foreach (var prefix in prefixes)
            {
                if (normA.StartsWith(prefix) && normB.StartsWith(prefix))
                    return true;
            }

            // Match pe cuvinte cheie (minim 40% overlap)
            var wordsA = normA.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                              .Where(w => w.Length > 4).ToHashSet();
            var wordsB = normB.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                              .Where(w => w.Length > 4).ToHashSet();

            if (wordsA.Count == 0 || wordsB.Count == 0) return false;

            int commonWords = wordsA.Count(w => wordsB.Contains(w));
            double overlapRatio = (double)commonWords / Math.Min(wordsA.Count, wordsB.Count);

            return overlapRatio >= 0.4;
        }

        private string TruncateString(string s, int maxLen) =>
            s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
    }
}