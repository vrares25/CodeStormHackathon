using CodeStormHackathon.Models;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace CodeStormHackathon.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;
        private const string OllamaUrl = "http://localhost:11434/api/generate";
        public AIService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(3);
        }
        public async Task<string> ExtractTextFromImageAsync(string imagePath)
        {
            byte[] imageBytes = System.IO.File.ReadAllBytes(imagePath);
            string base64Image = Convert.ToBase64String(imageBytes);
            var requestBody = new
            {
                model = "llama3.2-vision",
                prompt = "Extract all the text from this academic document. Preserve the structure of tables and lists.",
                images = new[] { base64Image },
                stream = false
            };
            return await SendOllamaRequestAsync(requestBody);
        }
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
            var requestBody = new
            {
                model = "gemma2",
                prompt = prompt,
                format = "json",
                stream = false
            };
            string jsonResponse = await SendOllamaRequestAsync(requestBody);
            try
            {
                return JsonSerializer.Deserialize<SyllabusData>(jsonResponse);
            }
            catch (Exception ex)
            {
                throw new Exception($"Gemma 2 a returnat un JSON invalid: {ex.Message}\nRăspuns brut: {jsonResponse}");
            }
        }
        public async Task<string> AskCopilotAsync(string context, string userPrompt)
        {
            string fullPrompt = $"Context din Fișa Disciplinei:\n{context}\n\nCerinta profesorului: {userPrompt}\n\nReformuleaza textul conform cerintei. Returneaza doar textul modificat.";
            var requestBody = new
            {
                model = "gemma2",
                prompt = fullPrompt,
                stream = false
            };

            return await SendOllamaRequestAsync(requestBody);
        }
        private async Task<string> SendOllamaRequestAsync(object requestBody)
        {
            string jsonRequest = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(OllamaUrl, content);
            response.EnsureSuccessStatusCode();
            string jsonResponse = await response.Content.ReadAsStringAsync();
            using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
            {
                return doc.RootElement.GetProperty("response").GetString();
            }
        }
    }
}
