using Clinic.Models.Enums;

namespace Clinic.Models.Dtos
{
    public class PatientDto
    {
        public int PatientId { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateOnly BirthDate { get; set; }

        public Gender Gender { get; set; }

        public string Phone { get; set; } = string.Empty;
    }
}
