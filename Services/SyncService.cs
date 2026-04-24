using CodeStormHackathon.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeStormHackathon.Services
{
    public class SyncService
    {
        public List<string> CompareWithPlan(SyllabusData fd, StudyPlanEntry planEntry)
        {
            var conflicts = new List<string>();
            if (fd.Credits != planEntry.Credits)
                conflicts.Add($"Conflict Credite: FD are {fd.Credits}, Planul are {planEntry.Credits}.");
            if (fd.EvaluationType != planEntry.EvaluationType)
                conflicts.Add($"Conflict Evaluare: FD zice {fd.EvaluationType}, Planul zice {planEntry.EvaluationType}.");

            return conflicts;
        }
    }
}
