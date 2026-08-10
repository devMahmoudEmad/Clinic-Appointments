using Clinic.Data;
using Clinic.Models.Dtos;
using Clinic.Models.Entities;
using Clinic.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Services
{
    public interface IDoctorService
    {
        Task<IReadOnlyList<DoctorDto>> GetDoctorsAsync();

        Task<PagedResult<DoctorDto>> GetDoctorsPagedAsync(int page, int pageSize);

        Task<DoctorDto?> GetDoctorAsync(int doctorId);

        Task<bool> DoctorExistsAsync(int doctorId);

        Task<int> CreateDoctorAsync(DoctorDto doctor);

        Task<bool> UpdateDoctorAsync(DoctorDto doctor);

        Task<IReadOnlyList<DoctorWeeklySchedule>> GetWeeklyScheduleAsync(int doctorId);

        Task<IReadOnlyList<ScheduleException>> GetScheduleExceptionsAsync(int doctorId);

        Task<bool> AddWeeklyScheduleAsync(int doctorId, DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime);

        Task<bool> RemoveWeeklyScheduleAsync(int doctorId, int scheduleId);

        Task<bool> AddScheduleExceptionAsync(
            int doctorId,
            DateOnly exceptionDate,
            ScheduleExceptionType type,
            TimeOnly? startTime,
            TimeOnly? endTime,
            string? reason);

        Task<bool> RemoveScheduleExceptionAsync(int doctorId, int exceptionId);
    }

    public class DoctorService : IDoctorService
    {
        private readonly ClinicDbContext _db;

        public DoctorService(ClinicDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<DoctorDto>> GetDoctorsAsync()
        {
            return await _db.Doctors.AsNoTracking()
                .OrderBy(d => d.Name)
                .Select(d => new DoctorDto
                {
                    DoctorId = d.DoctorId,
                    ClinicId = d.ClinicId,
                    ClinicName = d.Clinic!.Name,
                    Name = d.Name,
                    Specialization = d.Specialization,
                    Phone = d.Phone
                })
                .ToListAsync();
        }

        public async Task<PagedResult<DoctorDto>> GetDoctorsPagedAsync(int page, int pageSize)
        {
            var query = _db.Doctors.AsNoTracking();

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(d => d.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new DoctorDto
                {
                    DoctorId = d.DoctorId,
                    ClinicId = d.ClinicId,
                    ClinicName = d.Clinic!.Name,
                    Name = d.Name,
                    Specialization = d.Specialization,
                    Phone = d.Phone
                })
                .ToListAsync();

            return PagedResult<DoctorDto>.Create(items, page, pageSize, totalCount);
        }

        public async Task<DoctorDto?> GetDoctorAsync(int doctorId)
        {
            return await _db.Doctors.AsNoTracking()
                .Where(d => d.DoctorId == doctorId)
                .Select(d => new DoctorDto
                {
                    DoctorId = d.DoctorId,
                    ClinicId = d.ClinicId,
                    ClinicName = d.Clinic!.Name,
                    Name = d.Name,
                    Specialization = d.Specialization,
                    Phone = d.Phone
                })
                .FirstOrDefaultAsync();
        }

        public async Task<bool> DoctorExistsAsync(int doctorId)
        {
            return await _db.Doctors.AnyAsync(d => d.DoctorId == doctorId);
        }

        public async Task<int> CreateDoctorAsync(DoctorDto doctor)
        {
            var entity = new Doctor
            {
                ClinicId = doctor.ClinicId,
                Name = Normalize(doctor.Name),
                Specialization = Normalize(doctor.Specialization),
                Phone = Normalize(doctor.Phone)
            };

            _db.Doctors.Add(entity);
            await _db.SaveChangesAsync();
            return entity.DoctorId;
        }

        public async Task<bool> UpdateDoctorAsync(DoctorDto doctor)
        {
            var entity = await _db.Doctors
                .FirstOrDefaultAsync(d => d.DoctorId == doctor.DoctorId);

            if (entity is null)
            {
                return false;
            }

            entity.ClinicId = doctor.ClinicId;
            entity.Name = Normalize(doctor.Name);
            entity.Specialization = Normalize(doctor.Specialization);
            entity.Phone = Normalize(doctor.Phone);

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<IReadOnlyList<DoctorWeeklySchedule>> GetWeeklyScheduleAsync(int doctorId)
        {
            return await _db.DoctorWeeklySchedules.AsNoTracking()
                .Where(s => s.DoctorId == doctorId)
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.StartTime)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<ScheduleException>> GetScheduleExceptionsAsync(int doctorId)
        {
            return await _db.ScheduleExceptions.AsNoTracking()
                .Where(e => e.DoctorId == doctorId)
                .OrderBy(e => e.ExceptionDate)
                .ToListAsync();
        }

        public async Task<bool> AddWeeklyScheduleAsync(
            int doctorId,
            DayOfWeek dayOfWeek,
            TimeOnly startTime,
            TimeOnly endTime)
        {
            if (endTime <= startTime)
            {
                return false;
            }

            if (!await DoctorExistsAsync(doctorId))
            {
                return false;
            }

            var existing = await _db.DoctorWeeklySchedules
                .Where(s => s.DoctorId == doctorId && s.DayOfWeek == dayOfWeek)
                .ToListAsync();

            if (existing.Any(s =>
                ScheduleCalculator.Overlaps(startTime, endTime, s.StartTime, s.EndTime)))
            {
                return false;
            }

            _db.DoctorWeeklySchedules.Add(new DoctorWeeklySchedule
            {
                DoctorId = doctorId,
                DayOfWeek = dayOfWeek,
                StartTime = startTime,
                EndTime = endTime,
                IsActive = true
            });

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveWeeklyScheduleAsync(int doctorId, int scheduleId)
        {
            var schedule = await _db.DoctorWeeklySchedules
                .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId && s.DoctorId == doctorId);

            if (schedule is null)
            {
                return false;
            }

            _db.DoctorWeeklySchedules.Remove(schedule);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AddScheduleExceptionAsync(
            int doctorId,
            DateOnly exceptionDate,
            ScheduleExceptionType type,
            TimeOnly? startTime,
            TimeOnly? endTime,
            string? reason)
        {
            if (type == ScheduleExceptionType.ModifiedHours)
            {
                if (!startTime.HasValue || !endTime.HasValue || endTime <= startTime)
                {
                    return false;
                }
            }

            if (!await DoctorExistsAsync(doctorId))
            {
                return false;
            }

            if (exceptionDate < DateOnly.FromDateTime(DateTime.Today))
            {
                return false;
            }

            var exists = await _db.ScheduleExceptions
                .AnyAsync(e => e.DoctorId == doctorId && e.ExceptionDate == exceptionDate);

            if (exists)
            {
                return false;
            }

            _db.ScheduleExceptions.Add(new ScheduleException
            {
                DoctorId = doctorId,
                ExceptionDate = exceptionDate,
                ExceptionType = type,
                StartTime = startTime,
                EndTime = endTime,
                Reason = reason
            });

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveScheduleExceptionAsync(int doctorId, int exceptionId)
        {
            var exception = await _db.ScheduleExceptions
                .FirstOrDefaultAsync(e => e.ExceptionId == exceptionId && e.DoctorId == doctorId);

            if (exception is null)
            {
                return false;
            }

            _db.ScheduleExceptions.Remove(exception);
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
