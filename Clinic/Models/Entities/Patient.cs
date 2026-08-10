using Clinic.Models.Enums;

namespace Clinic.Models.Entities
{
    public class Patient
    {
        public int PatientId { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateOnly BirthDate { get; set; }

        public Gender Gender { get; set; }

        public string Phone { get; set; } = string.Empty;

        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
