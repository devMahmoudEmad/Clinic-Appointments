using Clinic.Models.Dtos;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Clinic.ViewModels.Appointments
{
    /// <summary>
    /// Read-only daily overview for a doctor: working periods, booked
    /// appointments, available start times and the next available slot.
    /// Holds only DTOs — never EF entities.
    /// </summary>
    public class DailyScheduleViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Please select a doctor.")]
        public int? DoctorId { get; set; }

        public string DoctorName { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Required(ErrorMessage = "Please select a date.")]
        public DateOnly? Date { get; set; }

        [AllowedAppointmentDurations]
        public int DurationMinutes { get; set; } = AllowedAppointmentDurationsAttribute.ThirtyMinutes;

        public bool IsWorking { get; set; }

        public bool IsDayOff { get; set; }

        public IReadOnlyList<WorkingPeriod> WorkingPeriods { get; set; } = Array.Empty<WorkingPeriod>();

        public IReadOnlyList<DailyScheduleRow> Rows { get; set; } = Array.Empty<DailyScheduleRow>();

        public TimeOnly? NextAvailableSlot { get; set; }

        public string? Message { get; set; }

        public List<SelectListItem> Doctors { get; set; } = new();
    }

    public class DailyScheduleRow
    {
        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }

        public string Status { get; set; } = string.Empty;

        public string PatientName { get; set; } = string.Empty;
    }
}
