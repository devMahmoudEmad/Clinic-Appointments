namespace Clinic.Models.Entities
{
    public class Doctor
    {
        public int DoctorId { get; set; }

        public int ClinicId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Specialization { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public Clinic? Clinic { get; set; }

        public ICollection<DoctorWeeklySchedule> WeeklySchedules { get; set; } = new List<DoctorWeeklySchedule>();

        public ICollection<ScheduleException> ScheduleExceptions { get; set; } = new List<ScheduleException>();

        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
