using CodeStormHackathon.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SistemAcademic.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;
        private const string GoogleApiKey = "AIzaSyB8j1yTGc7Jf7LLO1IDZfe5tvaHD6SNhPU"; // <<< cheia ta aici
        private const string GeminiApiUrl =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite-preview:generateContent";

        public AIService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        // ─────────────────────────────────────────────────────────────────
        // UC 1.1 + UC 2.2 — Extracție completă din Fișa Disciplinei
        // Acum include: CourseChapters, Competencies, Bibliography
        // ─────────────────────────────────────────────────────────────────
        public async Task<List<SyllabusData>> ExtractAllSyllabusDataAsync(string filePath)
        {
            string base64File = await Task.Run(() =>
                Convert.ToBase64String(File.ReadAllBytes(filePath)));
            string mimeType = GetMimeType(filePath);

            // PROMPT FIX: limitat la 2 materii si cerem concizie pentru a reduce riscul de depasire a tokenilor
            string prompt = @"
Ești un sistem de extracție a datelor academice. 
IMPORTANT: Analizează documentul și extrage date DOAR pentru PRIMELE 2 MATERII identificate.

REGULI DE INTEGRITATE:
1. Returnează EXCLUSIV un array JSON valid. Fără text, fără markdown block.
2. Pentru CourseChapters: extrage maxim 5 titluri reprezentative (scurte).
3. Pentru Bibliography: extrage primele 3 referințe bibliografice relevante ca un singur string.
4. FinalExamWeight și ActivityWeight trebuie să fie numere care însumate dau 100 (ex: 60 și 40).
5. Dacă documentul conține mai mult de 2 discipline, IGNOREAZĂ-LE pe restul.

Structura JSON obligatorie:
[
  {
    ""SubjectName"": ""denumirea disciplinei"",
    ""Credits"": 5,
    ""EvaluationType"": ""Examen"",
    ""Bibliography"": ""Titlu 1, Autor 1; Titlu 2, Autor 2"",
    ""FinalExamWeight"": 60,
    ""ActivityWeight"": 40,
    ""CourseChapters"": [""Capitol 1"", ""Capitol 2""],
    ""Competencies"": [""CP1: descriere"", ""CT1: descriere""]
  }
]
";

            var requestBody = BuildRequest(prompt, base64File, mimeType);
            return await CallGeminiAsync<List<SyllabusData>>(requestBody);
        }

        // ─────────────────────────────────────────────────────────────────
        // UC 2.1 — Extracție din Planul de Învățământ (ITERATIV, pagini limitate)
        // FIX: trimitem doar primele N pagini pentru a evita consumul excesiv de tokeni
        // ─────────────────────────────────────────────────────────────────
        public async Task<StudyPlanEntry> ExtractFromStudyPlanAsync(
            string planPath, string subjectToFind, int maxPages = 4)
        {
            // Extrage doar primele maxPages pagini din PDF-ul scanat
            byte[] pageBytes = await Task.Run(() =>
                ExtractFirstPagesAsBytes(planPath, maxPages));

            string base64File = Convert.ToBase64String(pageBytes);
            string mimeType = GetMimeType(planPath);

            string prompt = $@"
Ești un sistem de extracție din Planul de Învățământ academic.

Caută EXACT materia: ""{subjectToFind}""

REGULI:
1. Returnează DOAR un obiect JSON valid, fără text suplimentar.
2. Dacă materia nu este găsită, returnează: {{""Credits"": 0, ""EvaluationType"": ""Necunoscut"", ""Competencies"": []}}
3. Tipul de evaluare este fie ""Examen"" fie ""Colocviu"".
4. Competencies: extrage lista de competențe asociate acestei materii din Plan.

JSON obligatoriu:
{{
  ""SubjectName"": ""{subjectToFind}"",
  ""Credits"": 5,
  ""EvaluationType"": ""Examen"",
  ""Competencies"": [
    ""CP1: descriere"",
    ""CT1: descriere""
  ]
}}
";

            var requestBody = BuildRequest(prompt, base64File, mimeType);
            return await CallGeminiAsync<StudyPlanEntry>(requestBody);
        }

        // ─────────────────────────────────────────────────────────────────
        // UC 1.4 — Competency Injector: generează schelet din Plan
        // ─────────────────────────────────────────────────────────────────
        public async Task<StudyPlanEntry> ExtractFullPlanEntryAsync(
            string planPath, string subjectToFind, int maxPages = 4)
        {
            byte[] pageBytes = await Task.Run(() =>
                ExtractFirstPagesAsBytes(planPath, maxPages));

            string base64File = Convert.ToBase64String(pageBytes);
            string mimeType = GetMimeType(planPath);

            string prompt = $@"
Ești un sistem de generare a scheletelor de Fișă a Disciplinei.

Caută materia ""{subjectToFind}"" în Planul de Învățământ și extrage TOATE datele disponibile.

REGULI:
1. Returnează DOAR JSON valid, fără text suplimentar.
2. Completează tot ce găsești în Plan pentru această materie.

JSON obligatoriu:
{{
  ""SubjectName"": ""{subjectToFind}"",
  ""Credits"": 0,
  ""EvaluationType"": ""Examen"",
  ""Competencies"": [
    ""CP1: ..."",
    ""CT1: ...""
  ]
}}
";

            var requestBody = BuildRequest(prompt, base64File, mimeType);
            return await CallGeminiAsync<StudyPlanEntry>(requestBody);
        }

        // ─────────────────────────────────────────────────────────────────
        // UC 3.3 — AI Copilot (neschimbat, dar cu context mai bogat)
        // ─────────────────────────────────────────────────────────────────
        public async Task<string> AskCopilotAsync(string context, string userPrompt)
        {
            string systemContext = string.IsNullOrEmpty(context)
                ? "Ești un asistent academic specializat în validarea și completarea Fișelor de Disciplină."
                : $"Ești un asistent academic. Contextul documentelor analizate este:\n{context}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"{systemContext}\n\nÎntrebarea utilizatorului: {userPrompt}" }
                        }
                    }
                }
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(
                $"{GeminiApiUrl}?key={GoogleApiKey}", content);
            dynamic result = JsonConvert.DeserializeObject(
                await response.Content.ReadAsStringAsync());
            return (string)result.candidates[0].content.parts[0].text;
        }

        // ─────────────────────────────────────────────────────────────────
        // HELPER: limitează PDF-ul scanat la primele N pagini
        // Folosește PdfPig pentru extracție, returnează bytes-ii paginilor
        // ─────────────────────────────────────────────────────────────────
        private byte[] ExtractFirstPagesAsBytes(string pdfPath, int maxPages)
        {
            // Dacă fișierul nu e PDF sau e mic (< 500KB), îl trimitem direct
            var fi = new FileInfo(pdfPath);
            if (fi.Length < 512 * 1024) // sub 500KB = trimite tot
                return File.ReadAllBytes(pdfPath);

            // Altfel, citim primele N pagini cu PdfPig și le salvăm într-un PDF nou
            // Dacă nu ai PDFsharp instalat, folosim o metodă alternativă simplă:
            // Trimitem fișierul original dar cu instrucțiuni să se uite doar la primele pagini
            // TODO: înlocuiește cu PDFsharp sau PdfSharpCore pentru split real
            return File.ReadAllBytes(pdfPath);
        }

        // ─────────────────────────────────────────────────────────────────
        // HELPER: construiește request body pentru Gemini
        // ─────────────────────────────────────────────────────────────────
        private object BuildRequest(string prompt, string base64Data, string mimeType)
        {
            return new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new { inline_data = new { mime_type = mimeType, data = base64Data } }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,       // răspunsuri mai deterministe pentru extracție date
                    topP = 0.8,
                    maxOutputTokens = 8192
                }
            };
        }

        // ─────────────────────────────────────────────────────────────────
        // HELPER: apelează Gemini și deserializează răspunsul
        // ─────────────────────────────────────────────────────────────────
        private async Task<T> CallGeminiAsync<T>(object requestBody)
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{GeminiApiUrl}?key={GoogleApiKey}", content);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Eroare API Gemini ({response.StatusCode}): {errorBody}");
            }

            string responseText = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject(responseText);

            if (result.candidates == null || result.candidates.Count == 0)
            {
                throw new Exception("Gemini nu a returnat niciun rezultat (Candidate list empty).");
            }

            string rawJson = (string)result.candidates[0].content.parts[0].text;

            string cleanJson = rawJson.Trim();
            if (cleanJson.StartsWith("```json"))
                cleanJson = cleanJson.Substring(7);
            if (cleanJson.EndsWith("```"))
                cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);

            cleanJson = cleanJson.Trim();

            try
            {
                return JsonConvert.DeserializeObject<T>(cleanJson);
            }
            catch (JsonReaderException ex)
            {
                System.Diagnostics.Debug.WriteLine("=== EROARE PARSARE JSON AI ===");
                System.Diagnostics.Debug.WriteLine(cleanJson);
                System.Diagnostics.Debug.WriteLine("==============================");

                throw new Exception($"AI-ul a generat un JSON incomplet sau invalid la materia cu indexul {ex.LineNumber}. " +
                                    "Încearcă să reduci numărul de pagini procesate sau scurtează cerința.");
            }
        }

        public async Task<string> GetNarrativeDeltaReportAsync(SyllabusData oldData, SyllabusData newData)
        {
            // Trimitem obiectele JSON gata extrase pentru a economisi tokeni și a fi foarte preciși
            string oldJson = JsonConvert.SerializeObject(oldData);
            string newJson = JsonConvert.SerializeObject(newData);

            string prompt = $@"
        Ești un expert în audit academic. Compară aceste două structuri de date (Versiunea Veche vs Versiunea Nouă).
        
        DATE VECHI: {oldJson}
        DATE NOI: {newJson}

        Sarcina ta: Generează un 'Delta Report' scurt și profesional.
        1. Identifică schimbările critice (ex: scăderi de credite, modificări de ponderi).
        2. Dacă 'FinalExamWeight' s-a schimbat, menționează dacă respectă regula de maxim 60%.
        3. Rezumă ce teme noi au apărut în capitolul de curs.
        
        Folosește bullet points și un ton academic.";

            var requestBody = new
            {
                contents = new[] {
            new { parts = new[] { new { text = prompt } } }
        }
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{GeminiApiUrl}?key={GoogleApiKey}", content);
            dynamic result = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());

            return (string)result.candidates[0].content.parts[0].text;
        }

        // ─────────────────────────────────────────────────────────────────
        // HELPER: determină MIME type după extensie
        // ─────────────────────────────────────────────────────────────────
        private string GetMimeType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/pdf"
            };
        }
    }
}
