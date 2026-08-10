namespace Clinic.Models.Dtos
{
    public record CreateAppointmentCommand(
        int PatientId,
        int DoctorId,
        DateOnly AppointmentDate,
        TimeOnly StartTime,
        int DurationMinutes);

    public record UpdateAppointmentCommand(
        int AppointmentId,
        int PatientId,
        int DoctorId,
        DateOnly AppointmentDate,
        TimeOnly StartTime,
        int DurationMinutes);

    public record AppointmentCreationResult(bool Success, string? ErrorMessage)
    {
        public static AppointmentCreationResult Ok() => new(true, null);

        public static AppointmentCreationResult Fail(string errorMessage) => new(false, errorMessage);
    }
}
