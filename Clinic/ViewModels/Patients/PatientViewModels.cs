using Clinic.Models.Dtos;
using Clinic.Models.Enums;

namespace Clinic.ViewModels.Patients
{
    public class PatientSearchViewModel : IPagedViewModel
    {
        public string? SearchTerm { get; set; }

        public IReadOnlyList<PatientDto> Patients { get; set; } = new List<PatientDto>();

        public int CurrentPage { get; set; } = 1;

        public int TotalPages { get; set; }
    }

    public class PatientCreateViewModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Patient name is required.")]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Birth date is required.")]
        [Clinic.Validation.DateNotInFuture]
        public DateOnly? BirthDate { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Please select a gender.")]
        public Gender? Gender { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Phone number is required.")]
        [System.ComponentModel.DataAnnotations.Phone(ErrorMessage = "Enter a valid phone number.")]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string Phone { get; set; } = string.Empty;
    }

    public class PatientEditViewModel
    {
        public int PatientId { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Patient name is required.")]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Birth date is required.")]
        [Clinic.Validation.DateNotInFuture]
        public DateOnly? BirthDate { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Please select a gender.")]
        public Gender? Gender { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Phone number is required.")]
        [System.ComponentModel.DataAnnotations.Phone(ErrorMessage = "Enter a valid phone number.")]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string Phone { get; set; } = string.Empty;
    }
}
