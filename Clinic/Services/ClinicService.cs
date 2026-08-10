using Clinic.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Services
{
    public interface IClinicService
    {
        Task<IReadOnlyList<(int ClinicId, string Name)>> GetClinicsAsync();
    }

    public class ClinicService : IClinicService
    {
        private readonly ClinicDbContext _db;

        public ClinicService(ClinicDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<(int ClinicId, string Name)>> GetClinicsAsync()
        {
            var clinics = await _db.Clinics.AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new { c.ClinicId, c.Name })
                .ToListAsync();

            return clinics
                .Select(c => (c.ClinicId, c.Name))
                .ToList();
        }
    }
}
