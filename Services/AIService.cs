using CodeStormHackathon.Models;
using Newtonsoft.Json; 
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SistemAcademic.Services 
{
    public class AIService
    {
        private readonly HttpClient _httpClient;

        private const string GoogleApiKey = "";


        private const string GoogleApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemma-3-27b-it:generateContent";

        // --- CONFIGURARE OLLAMA LOCAL (LLAMA VISION) ---
        private const string OllamaUrl = "http://localhost:11434/api/generate";

        public AIService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(3); // Modelele pot dura la procesare
        }

        // =========================================================================
        // 1. LLAMA VISION (OLLAMA LOCAL) - Extragere text din imagini
        // =========================================================================
        public async Task<string> ExtractTextFromImageAsync(string imagePath)
        {
            byte[] imageBytes = File.ReadAllBytes(imagePath);
            string base64Image = Convert.ToBase64String(imageBytes);

            var requestBody = new
            {
                model = "llama3.2-vision",
                prompt = "Extract all the text from this academic document. Preserve the structure of tables and lists. Return ONLY the text, no other comments.",
                images = new[] { base64Image },
                stream = false
            };

            string jsonRequest = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(OllamaUrl, content);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject(jsonResponse);

            return result.response;
        }

        // =========================================================================
        // 2. GEMMA (GOOGLE API) - Parsare text brut în JSON (Nivel 4)
        // =========================================================================
        public async Task<SyllabusData> ParseTextToDataAsync(string rawText)
        {
            string prompt = $@"
            You are an academic data extractor. Extract the following information from the text and return it STRICTLY as a JSON object matching this schema, with no markdown formatting or other text:
            {{
                ""SubjectName"": ""string"",
                ""Credits"": number,
                ""EvaluationType"": ""string (Examen or Colocviu)"",
                ""Bibliography"": ""string"",
                ""FinalExamWeight"": number (0 to 100),
                ""ActivityWeight"": number (0 to 100)
            }}

            Document Text:
            {rawText}";

            string aiResponseText = await SendGoogleApiRequestAsync(prompt);

            try
            {
                // De multe ori API-urile mai pun block-uri de cod gen ```json ... ``` 
                // Așa că le curățăm înainte de deserializare
                string cleanJson = aiResponseText.Replace("```json", "").Replace("```", "").Trim();

                return JsonConvert.DeserializeObject<SyllabusData>(cleanJson);
            }
            catch (Exception ex)
            {
                throw new Exception($"Gemma a returnat un JSON invalid: {ex.Message}\n\nRăspuns brut: {aiResponseText}");
            }
        }

        // =========================================================================
        // 3. GEMMA (GOOGLE API) - Academic Copilot Chat (Nivel 3)
        // =========================================================================
        public async Task<string> AskCopilotAsync(string context, string userPrompt)
        {
            string fullPrompt = $"Context din Fișa Disciplinei:\n{context}\n\nCerinta profesorului: {userPrompt}\n\nAcționează ca un asistent academic. Reformuleaza textul conform cerintei. Returneaza doar textul modificat/recomandat.";

            return await SendGoogleApiRequestAsync(fullPrompt);
        }

        // =========================================================================
        // METODĂ INTERNĂ: Helper pentru a face call-ul către Google AI Studio
        // =========================================================================
        private async Task<string> SendGoogleApiRequestAsync(string promptText)
        {
            // Construim JSON-ul fix in formatul cerut de Google API
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = promptText } }
                    }
                }
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{GoogleApiUrl}?key={GoogleApiKey}", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                throw new Exception($"Eroare API Google ({response.StatusCode}): {errorDetails}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject(jsonResponse);

            // Extragem textul răspunsului din ierarhia JSON-ului Google
            return result.candidates[0].content.parts[0].text;
        }
    }
}