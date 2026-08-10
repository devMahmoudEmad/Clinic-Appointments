namespace Clinic.Models.Entities
{
    public class DoctorWeeklySchedule
    {
        public int ScheduleId { get; set; }

        public int DoctorId { get; set; }

        public DayOfWeek DayOfWeek { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }

        public bool IsActive { get; set; } = true;

        public Doctor? Doctor { get; set; }
    }
}
