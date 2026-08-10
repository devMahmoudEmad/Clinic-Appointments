using Clinic.Models.Dtos;
using Clinic.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Clinic.ViewModels.Appointments
{
    public class AppointmentIndexViewModel : IPagedViewModel
    {
        [DataType(DataType.Date)]
        public DateOnly? Date { get; set; }

        public int? DoctorId { get; set; }

        public string? PatientName { get; set; }

        public AppointmentStatus? Status { get; set; }

        public List<AppointmentRowViewModel> Appointments { get; set; } = new();

        public List<SelectListItem> Doctors { get; set; } = new();

        public int CurrentPage { get; set; } = 1;

        public int TotalPages { get; set; }
    }

    public class AppointmentRowViewModel
    {
        public int AppointmentId { get; set; }

        public DateOnly AppointmentDate { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }

        public string Status { get; set; } = string.Empty;

        public string PatientName { get; set; } = string.Empty;

        public string DoctorName { get; set; } = string.Empty;
    }
}
