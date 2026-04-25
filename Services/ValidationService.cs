using CodeStormHackathon.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodeStormHackathon.Services
{
    public class ValidationService
    {
        // ─────────────────────────────────────────────────────────────────
        // UC 1.1 — Integrity Guard: verifică câmpurile obligatorii
        // ─────────────────────────────────────────────────────────────────
        public List<ValidationResult> CheckIntegrity(SyllabusData fd)
        {
            var results = new List<ValidationResult>();

            // Denumire materie
            if (string.IsNullOrWhiteSpace(fd.SubjectName))
                results.Add(ValidationResult.Error(
                    "UC1.1", "Denumirea disciplinei lipsește."));

            // Bibliografie
            if (string.IsNullOrWhiteSpace(fd.Bibliography))
                results.Add(ValidationResult.Error(
                    "UC1.1", "Bibliografia este goală."));
            else if (fd.Bibliography.Length < 20)
                results.Add(ValidationResult.Warning(
                    "UC1.1", $"Bibliografia pare incompletă: \"{fd.Bibliography}\""));

            // Capitole curs (tematică)
            if (fd.CourseChapters == null || fd.CourseChapters.Count == 0)
                results.Add(ValidationResult.Error(
                    "UC1.1", "Tematica cursului (capitolele) lipsește complet."));
            else if (fd.CourseChapters.Count < 3)
                results.Add(ValidationResult.Warning(
                    "UC1.1", $"Tematica are doar {fd.CourseChapters.Count} capitol(e). Se recomandă minim 3."));

            // Competențe
            if (fd.Competencies == null || fd.Competencies.Count == 0)
                results.Add(ValidationResult.Error(
                    "UC1.1", "Competențele (CP/CT) lipsesc din fișă."));

            // Tip evaluare
            if (string.IsNullOrWhiteSpace(fd.EvaluationType))
                results.Add(ValidationResult.Error(
                    "UC1.1", "Tipul de evaluare (Examen/Colocviu) lipsește."));
            else if (fd.EvaluationType != "Examen" && fd.EvaluationType != "Colocviu")
                results.Add(ValidationResult.Warning(
                    "UC1.1", $"Tip evaluare nerecunoscut: \"{fd.EvaluationType}\". Valori acceptate: Examen, Colocviu."));

            // Credite
            if (fd.Credits <= 0)
                results.Add(ValidationResult.Error(
                    "UC1.1", "Numărul de credite lipsește sau este invalid (0)."));

            if (results.Count == 0)
                results.Add(ValidationResult.Success(
                    "UC1.1", $"[{fd.SubjectName}] Integritate structurală OK — toate câmpurile obligatorii sunt prezente."));

            return results;
        }

        // ─────────────────────────────────────────────────────────────────
        // UC 1.2 — Math Checker: verifică ponderile evaluării
        // ─────────────────────────────────────────────────────────────────
        public List<ValidationResult> ValidateWeights(SyllabusData fd)
        {
            var results = new List<ValidationResult>();

            // Verifică dacă ponderile au fost extrase deloc
            if (fd.FinalExamWeight == 0 && fd.ActivityWeight == 0)
            {
                results.Add(ValidationResult.Warning(
                    "UC1.2", $"[{fd.SubjectName}] Ponderile sunt ambele 0 — posibil neextrase din document."));
                return results;
            }

            double total = fd.FinalExamWeight + fd.ActivityWeight;

            // Suma trebuie să fie 100%
            if (Math.Abs(total - 100) > 0.01)
                results.Add(ValidationResult.Error(
                    "UC1.2", $"[{fd.SubjectName}] Ponderile însumează {total}%, nu 100%. " +
                             $"(Examen: {fd.FinalExamWeight}% + Activitate: {fd.ActivityWeight}%)"));
            else
                results.Add(ValidationResult.Success(
                    "UC1.2", $"[{fd.SubjectName}] Suma ponderilor OK: {fd.FinalExamWeight}% + {fd.ActivityWeight}% = 100%."));

            // Limita regulament: examenul nu poate depăși 60%
            if (fd.FinalExamWeight > 60)
                results.Add(ValidationResult.Error(
                    "UC1.2", $"[{fd.SubjectName}] Examenul final are {fd.FinalExamWeight}% — depășește limita regulamentului de 60%."));

            // Activitatea nu poate fi 0 dacă există examen
            if (fd.ActivityWeight == 0 && fd.FinalExamWeight > 0)
                results.Add(ValidationResult.Warning(
                    "UC1.2", $"[{fd.SubjectName}] Ponderea activității este 0%. Verificați dacă este corect."));

            return results;
        }

        // ─────────────────────────────────────────────────────────────────
        // Metodă combinată: rulează toate verificările Level 1
        // ─────────────────────────────────────────────────────────────────
        public List<ValidationResult> RunAllChecks(SyllabusData data)
        {
            var allResults = new List<ValidationResult>();

            // (Aici ai verificările tale vechi de la Nivelul 1 - Integritate & Matematică)
            // ex: allResults.AddRange(RunMathChecker(data));

            // ★ NOU: Rulăm UC 3.1 - Content Auditor ★
            allResults.AddRange(RunContentAuditor(data));

            return allResults;
        }

        // UC 3.1 - Analiza Calitativă și Semantică
        private List<ValidationResult> RunContentAuditor(SyllabusData data)
        {
            var results = new List<ValidationResult>();

            // 1. Detectare conținut generic (Titluri vagi)
            string[] vagueTerms = { "introducere", "curs 1", "curs 2", "proiect", "laborator", "recapitulare", "concluzii" };

            if (data.CourseChapters != null && data.CourseChapters.Count > 0)
            {
                foreach (var chapter in data.CourseChapters)
                {
                    string cleanChapter = chapter.ToLower().Trim();
                    if (vagueTerms.Any(term => cleanChapter == term || cleanChapter.StartsWith(term + ":") || cleanChapter.StartsWith(term + " -")))
                    {
                        results.Add(ValidationResult.Warning("CONTENT AUDITOR",
                            $"Capitolul '{chapter}' este prea generic. Detaliați tematica (ex: nu folosiți doar cuvântul 'Introducere')."));
                    }
                }
            }

            // 2. Actualitatea Bibliografiei (Minim o sursă din ultimii 5 ani)
            if (!string.IsNullOrEmpty(data.Bibliography))
            {
                // Căutăm ani de 4 cifre (ex: 2019, 2024)
                var matches = Regex.Matches(data.Bibliography, @"\b(19|20)\d{2}\b");
                bool hasRecentSource = false;

                int currentYear = 2026; // Conform cerințelor și contextului actual

                foreach (Match match in matches)
                {
                    if (int.TryParse(match.Value, out int year))
                    {
                        if (currentYear - year <= 5 && year <= currentYear)
                        {
                            hasRecentSource = true;
                            break;
                        }
                    }
                }

                if (!hasRecentSource)
                {
                    results.Add(ValidationResult.Warning("CONTENT AUDITOR",
                        $"Bibliografia este învechită. Nu există surse publicate în ultimii 5 ani ({currentYear - 5}-{currentYear})."));
                }
                else
                {
                    results.Add(ValidationResult.Success("CONTENT AUDITOR", "Bibliografia conține surse de actualitate."));
                }
            }
            else
            {
                results.Add(ValidationResult.Error("CONTENT AUDITOR", "Secțiunea de Bibliografie lipsește complet."));
            }

            return results;
        }

    }

    // ─────────────────────────────────────────────────────────────────
    // Model rezultat validare — înlocuiește string-urile simple
    // ─────────────────────────────────────────────────────────────────
    public class ValidationResult
    {
        public ValidationSeverity Severity { get; set; }
        public string UseCase { get; set; }   // ex: "UC1.1", "UC2.1"
        public string Message { get; set; }

        public static ValidationResult Error(string useCase, string msg) =>
            new ValidationResult { Severity = ValidationSeverity.Error, UseCase = useCase, Message = msg };

        public static ValidationResult Warning(string useCase, string msg) =>
            new ValidationResult { Severity = ValidationSeverity.Warning, UseCase = useCase, Message = msg };

        public static ValidationResult Success(string useCase, string msg) =>
            new ValidationResult { Severity = ValidationSeverity.Success, UseCase = useCase, Message = msg };
    }

    public enum ValidationSeverity { Success, Warning, Error }
}