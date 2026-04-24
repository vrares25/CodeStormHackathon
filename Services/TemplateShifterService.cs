using DocumentFormat.OpenXml.Packaging;
using System.IO;
using System;
using CodeStormHackathon.Models;

namespace CodeStormHackathon.Services
{
    public class TemplateShifterService
    {
        public void GenerateNewSyllabus(SyllabusData data, string templatePath, string outputPath)
        {
            try
            {
                File.Copy(templatePath, outputPath, true);

                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(outputPath, true))
                {
                    string docText = null;
                    using (StreamReader sr = new StreamReader(wordDoc.MainDocumentPart.GetStream()))
                    {
                        docText = sr.ReadToEnd();
                    }
                    docText = docText.Replace("[NUME_MATERIE]", EscapeXml(data.SubjectName));
                    docText = docText.Replace("[CREDITE]", data.Credits.ToString());
                    docText = docText.Replace("[EVALUARE]", EscapeXml(data.EvaluationType));

                    string biblio = string.IsNullOrEmpty(data.Bibliography)
                                    ? "[PLACEHOLDER: Vă rugăm detaliați bibliografia]"
                                    : EscapeXml(data.Bibliography);
                    docText = docText.Replace("[BIBLIOGRAFIE]", biblio);
                    docText = docText.Replace("[AN_UNIVERSITAR]", "2026-2027");
                    docText = docText.Replace("[DECAN]", "Prof. Univ. Dr. Popescu Ion");

                    using (StreamWriter sw = new StreamWriter(wordDoc.MainDocumentPart.GetStream(FileMode.Create)))
                    {
                        sw.Write(docText);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Eroare la generarea documentului: {ex.Message}");
            }
        }

        private string EscapeXml(string unescaped)
        {
            if (string.IsNullOrEmpty(unescaped)) return "";
            return unescaped.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                            .Replace("\"", "&quot;").Replace("'", "&apos;");
        }
    }
}