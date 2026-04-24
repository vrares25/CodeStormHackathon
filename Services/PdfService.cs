using UglyToad.PdfPig;
using System.Text;
using System.Linq;

public class PdfService
{
    public string ExtractText(string filePath, out bool isPossiblyScanned)
    {
        StringBuilder sb = new StringBuilder();
        isPossiblyScanned = false;

        using (var pdf = PdfDocument.Open(filePath))
        {
            foreach (var page in pdf.GetPages())
            {
                string pageText = page.Text;
                sb.AppendLine(pageText);
            }
        }

        string result = sb.ToString();
        if (string.IsNullOrWhiteSpace(result) || result.Trim().Length < 50)
        {
            isPossiblyScanned = true;
        }

        return result;
    }
}