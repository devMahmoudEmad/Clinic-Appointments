namespace Clinic.Models.Entities
{
    public class Clinic
    {
        public int ClinicId { get; set; }

        public string Name { get; set; } = string.Empty;

        public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
    }
}
