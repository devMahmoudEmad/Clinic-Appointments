using Clinic.Models.Enums;

namespace Clinic.Models.Dtos
{
    /// <summary>
    /// A booked appointment row for the daily schedule views.
    /// Only the patient name is surfaced; no other patient details are exposed.
    /// </summary>
    public sealed record DailyAppointmentDto(TimeOnly StartTime, TimeOnly EndTime, string PatientName, AppointmentStatus Status);
}
