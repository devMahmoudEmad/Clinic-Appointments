namespace Clinic.Models.Dtos
{
    /// <summary>
    /// Everything the read-only daily schedule view needs for one doctor/date,
    /// computed in a single pass: effective working periods, booked appointments,
    /// candidate slots on the 15-minute grid and the next available start.
    /// </summary>
    public sealed record DoctorDailyScheduleDto(
        bool IsWorking,
        bool IsDayOff,
        IReadOnlyList<WorkingPeriod> WorkingPeriods,
        IReadOnlyList<DailyAppointmentDto> Appointments,
        IReadOnlyList<TimeSlotDto> Slots,
        TimeOnly? NextAvailableStart,
        string? Message);
}
