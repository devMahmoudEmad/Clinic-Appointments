using Clinic.Data;
using Clinic.Models.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Services
{
    public interface IPatientService
    {
        Task<PatientDto?> GetPatientAsync(int patientId);

        Task<IReadOnlyList<PatientDto>> SearchPatientsAsync(string? searchTerm, int limit = 50);

        Task<PagedResult<PatientDto>> GetPagedAsync(string? searchTerm, int page, int pageSize);

        Task<int> CreatePatientAsync(PatientDto patient);

        Task<bool> UpdatePatientAsync(PatientDto patient);
    }

    public class PatientService : IPatientService
    {
        private readonly ClinicDbContext _db;

        public PatientService(ClinicDbContext db)
        {
            _db = db;
        }

        public async Task<PatientDto?> GetPatientAsync(int patientId)
        {
            return await _db.Patients.AsNoTracking()
                .Where(p => p.PatientId == patientId)
                .Select(p => new PatientDto
                {
                    PatientId = p.PatientId,
                    Name = p.Name,
                    BirthDate = p.BirthDate,
                    Gender = p.Gender,
                    Phone = p.Phone
                })
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<PatientDto>> SearchPatientsAsync(string? searchTerm, int limit = 50)
        {
            var query = _db.Patients.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p =>
                    p.Name.Contains(searchTerm) || p.Phone.Contains(searchTerm));
            }

            return await query
                .OrderBy(p => p.Name)
                .Take(limit)
                .Select(p => new PatientDto
                {
                    PatientId = p.PatientId,
                    Name = p.Name,
                    BirthDate = p.BirthDate,
                    Gender = p.Gender,
                    Phone = p.Phone
                })
                .ToListAsync();
        }

        public async Task<PagedResult<PatientDto>> GetPagedAsync(string? searchTerm, int page, int pageSize)
        {
            var query = _db.Patients.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p =>
                    p.Name.Contains(searchTerm) || p.Phone.Contains(searchTerm));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PatientDto
                {
                    PatientId = p.PatientId,
                    Name = p.Name,
                    BirthDate = p.BirthDate,
                    Gender = p.Gender,
                    Phone = p.Phone
                })
                .ToListAsync();

            return PagedResult<PatientDto>.Create(items, page, pageSize, totalCount);
        }

        public async Task<int> CreatePatientAsync(PatientDto patient)
        {
            var entity = new Models.Entities.Patient
            {
                Name = Normalize(patient.Name),
                BirthDate = patient.BirthDate,
                Gender = patient.Gender,
                Phone = Normalize(patient.Phone)
            };

            _db.Patients.Add(entity);
            await _db.SaveChangesAsync();
            return entity.PatientId;
        }

        public async Task<bool> UpdatePatientAsync(PatientDto patient)
        {
            var entity = await _db.Patients
                .FirstOrDefaultAsync(p => p.PatientId == patient.PatientId);

            if (entity is null)
            {
                return false;
            }

            entity.Name = Normalize(patient.Name);
            entity.BirthDate = patient.BirthDate;
            entity.Gender = patient.Gender;
            entity.Phone = Normalize(patient.Phone);

            await _db.SaveChangesAsync();
            return true;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
