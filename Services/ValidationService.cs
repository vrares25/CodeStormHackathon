using CodeStormHackathon.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeStormHackathon.Services
{
    public class ValidationService
    {
        public List<string> CheckIntegrity(SyllabusData fd)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(fd.Bibliography))
                errors.Add("Eroare: Bibliografia este goală.");

            if (fd.CourseChapters.Count == 0)
                errors.Add("Eroare: Lipsesc capitolele de curs (tematica).");

            return errors;
        }
        public bool ValidateWeights(SyllabusData fd, out string message)
        {
            double total = fd.FinalExamWeight + fd.ActivityWeight;
            if (total != 100)
            {
                message = $"Eroare Math: Ponderile însumează {total}%, nu 100%.";
                return false;
            }

            if (fd.FinalExamWeight > 60)
            {
                message = "Atenție: Conform regulamentului, examenul nu poate depăși 60%.";
                return false;
            }

            message = "Validare matematică reușită.";
            return true;
        }
    }
}
