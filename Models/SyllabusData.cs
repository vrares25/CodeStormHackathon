using System.Collections.Generic;

namespace CodeStormHackathon.Models
{
    public class SyllabusData
    {
        public string SubjectName { get; set; }
        public int Credits { get; set; }
        public string EvaluationType { get; set; }
        public string Bibliography { get; set; }
        public double FinalExamWeight { get; set; }
        public double ActivityWeight { get; set; }
        public List<string> CourseChapters { get; set; } = new List<string>();
        public List<string> Competencies { get; set; } = new List<string>();
    }
}