using Clinic.Models.Enums;

namespace Clinic.Models.Entities
{
    public class ScheduleException
    {
        public int ExceptionId { get; set; }

        public int DoctorId { get; set; }

        public DateOnly ExceptionDate { get; set; }

        public ScheduleExceptionType ExceptionType { get; set; }

        public TimeOnly? StartTime { get; set; }

        public TimeOnly? EndTime { get; set; }

        public string? Reason { get; set; }

        public Doctor? Doctor { get; set; }
    }
}
