using System;
using System.Collections.Generic;
using System.Text;

namespace CodeStormHackathon.Models
{
    public class StudyPlanEntry
    {
        public string SubjectName { get; set; }
        public int Credits { get; set; }
        public string EvaluationType { get; set; }
        public List<string> Competencies { get; set; } = new List<string>();
    }
}
