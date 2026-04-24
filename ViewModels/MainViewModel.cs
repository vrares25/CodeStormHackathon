using CodeStormHackathon.Models;
using CodeStormHackathon.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeStormHackathon.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        public void ProcessDocument(SyllabusData currentFd, StudyPlanEntry officialPlan)
        {
            var validator = new ValidationService();
            var sync = new SyncService();
            var errors = validator.CheckIntegrity(currentFd);
            if (!validator.ValidateWeights(currentFd, out string mathMsg))
                errors.Add(mathMsg);
            var conflicts = sync.CompareWithPlan(currentFd, officialPlan);
            errors.AddRange(conflicts);

            if (errors.Any())
                StatusMessage = string.Join("\n", errors);
            else
                StatusMessage = "Documentul este valid conform standardelor.";
        }
    }
}
