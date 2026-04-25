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

            // 1. Obținem erorile de integritate
            var errors = validator.CheckIntegrity(currentFd);

            // 2. Obținem erorile de calcul (Presupunând că ValidateWeights returnează List<string> acum)
            // Dacă returnează List<ValidationResult>, folosim .Select(x => x.Message)
            var mathErrors = validator.ValidateWeights(currentFd);
            if (mathErrors != null && mathErrors.Any())
            {
                errors.AddRange(mathErrors);
            }

            // 3. Obținem conflictele de sincronizare
            var conflicts = sync.CompareWithPlan(currentFd, officialPlan);
            errors.AddRange(conflicts);

            if (errors.Any())
                StatusMessage = string.Join("\n", errors);
            else
                StatusMessage = "Documentul este valid conform standardelor.";
        }
    }
}
