using CodeStormHackathon.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;
using UglyToad.PdfPig;
namespace CodeStormHackathon.Services
{
    public class WordReaderService
    {
        public string ExtractTextFromDocx(string filePath)
        {
            StringBuilder textBuilder = new StringBuilder();
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(filePath, false))
            {
                var body = wordDoc.MainDocumentPart.Document.Body;
                foreach (var para in body.Elements<Paragraph>())
                {
                    textBuilder.AppendLine(para.InnerText);
                }
            }
            return textBuilder.ToString();
        }

        public SyllabusData ParseSyllabusMock(string rawText)
        {
            return new SyllabusData
            {
                SubjectName = "Sisteme Inteligente",
                Credits = 5,
                EvaluationType = "Colocviu",
                Bibliography = "1. Russell & Norvig - AI",
                FinalExamWeight = 70,
                ActivityWeight = 30,
                CourseChapters = new List<string> { "Curs 1: Intro" }
            };
        }
        public string ExtractTextFromPdfDirect(string filePath)
        {
            using (var pdf = UglyToad.PdfPig.PdfDocument.Open(filePath))
            {
                StringBuilder sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    sb.Append(page.Text);
                }
                return sb.ToString();
            }
        }
    }
}
