namespace Clinic.Models.Dtos
{
    public class DoctorDto
    {
        public int DoctorId { get; set; }

        public int ClinicId { get; set; }

        public string ClinicName { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Specialization { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;
    }
}
