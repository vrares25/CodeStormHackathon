using System.Collections.Generic;

namespace CodeStormHackathon.Models 
{
    public class AiValidationReport
    {
        public List<string> IntegrityErrors { get; set; } = new List<string>();
        public List<string> MathErrors { get; set; } = new List<string>();
        public List<string> SyncErrors { get; set; } = new List<string>();
        public bool IsValid { get; set; }
    }
}