using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Clinic.ViewModels.Appointments
{
    public class AppointmentEditViewModel
    {
        public int AppointmentId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select a patient.")]
        public int PatientId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select a doctor.")]
        public int DoctorId { get; set; }

        [DataType(DataType.Date)]
        [Required(ErrorMessage = "Please select an appointment date.")]
        public DateOnly? AppointmentDate { get; set; }

        [Required(ErrorMessage = "Please select an appointment time.")]
        public TimeOnly? StartTime { get; set; }

        [AllowedAppointmentDurations]
        public int DurationMinutes { get; set; } = AllowedAppointmentDurationsAttribute.ThirtyMinutes;

        public string PatientName { get; set; } = string.Empty;

        public List<SelectListItem> Doctors { get; set; } = new();
    }
}
