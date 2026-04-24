using CodeStormHackathon.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        private const string GeminiApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        public AIService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        public async Task<List<SyllabusData>> ExtractAllSyllabusDataAsync(string filePath)
        {
            string base64File = await Task.Run(() => Convert.ToBase64String(File.ReadAllBytes(filePath)));
            string extension = Path.GetExtension(filePath).ToLower();
            string mimeType = extension == ".pdf" ? "application/pdf" : "image/jpeg";

            string prompt = @"Analizează documentul și extrage TOATE materiile identificate. 
            Returnează DOAR un array JSON cu structura:
            [
              {
                ""SubjectName"": ""string"",
                ""Credits"": number,
                ""EvaluationType"": ""Examen/Colocviu"",
                ""Bibliography"": ""string"",
                ""FinalExamWeight"": number,
                ""ActivityWeight"": number
              }
            ]";

            var requestBody = new
            {
                contents = new[] {
                    new {
                        parts = new object[] {
                            new { text = prompt },
                            new { inline_data = new { mime_type = mimeType, data = base64File } }
                        }
                    }
                }
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{GeminiApiUrl}?key={GoogleApiKey}", content);
            var responseText = await response.Content.ReadAsStringAsync();

            dynamic result = JsonConvert.DeserializeObject(responseText);
            string rawJson = result.candidates[0].content.parts[0].text;
            string cleanJson = rawJson.Replace("```json", "").Replace("```", "").Trim();

            return JsonConvert.DeserializeObject<List<SyllabusData>>(cleanJson);
        }

        public async Task<StudyPlanEntry> ExtractFromStudyPlanAsync(string planPath, string subjectToFind)
        {
            string base64File = await Task.Run(() => Convert.ToBase64String(File.ReadAllBytes(planPath)));
            string prompt = $"Caută materia '{subjectToFind}' în acest Plan de Învățământ. Returnează DOAR JSON: {{ \"Credits\": 0, \"EvaluationType\": \"string\" }}";

            var requestBody = new
            {
                contents = new[] {
                    new {
                        parts = new object[] {
                            new { text = prompt },
                            new { inline_data = new { mime_type = "application/pdf", data = base64File } }
                        }
                    }
                }
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{GeminiApiUrl}?key={GoogleApiKey}", content);
            var responseText = await response.Content.ReadAsStringAsync();

            dynamic result = JsonConvert.DeserializeObject(responseText);
            string cleanJson = ((string)result.candidates[0].content.parts[0].text).Replace("```json", "").Replace("```", "").Trim();
            return JsonConvert.DeserializeObject<StudyPlanEntry>(cleanJson);
        }

        public async Task<string> AskCopilotAsync(string context, string userPrompt)
        {
            var requestBody = new { contents = new[] { new { parts = new[] { new { text = $"CONTEXT: {context}\n\nUSER: {userPrompt}" } } } } };
            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{GeminiApiUrl}?key={GoogleApiKey}", content);
            dynamic result = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
            return result.candidates[0].content.parts[0].text;
        }
    }
}