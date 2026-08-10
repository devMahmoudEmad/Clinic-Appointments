using Clinic.Data;
using Clinic.Models.Dtos;
using Clinic.Models.Entities;
using Clinic.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Services
{
    public interface IScheduleService
    {
        Task<IReadOnlyList<WorkingPeriod>> GetEffectiveWorkingPeriodsAsync(int doctorId, DateOnly date);

        Task<IReadOnlyList<TimeSlotDto>> GetAvailableSlotsAsync(
            int doctorId,
            DateOnly date,
            int durationMinutes,
            DateTime? now = null);

        Task<AvailableSlotsResponseDto> GetAvailableSlotsResponseAsync(
            int doctorId,
            DateOnly date,
            int durationMinutes,
            string doctorName,
            DateTime? now = null);

        Task<DoctorDailyScheduleDto?> GetDailyScheduleAsync(
            int doctorId,
            string doctorName,
            DateOnly date,
            int durationMinutes,
            DateTime? now = null);

        Task<bool> IsDoctorAvailableAsync(int doctorId, DateOnly date, TimeOnly startTime, int durationMinutes);
    }

    public class ScheduleService : IScheduleService
    {
        private readonly ClinicDbContext _db;

        public ScheduleService(ClinicDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<WorkingPeriod>> GetEffectiveWorkingPeriodsAsync(int doctorId, DateOnly date)
        {
            var (periods, _) = await GetEffectiveWorkingPeriodsCoreAsync(doctorId, date);
            return periods;
        }

        public async Task<IReadOnlyList<TimeSlotDto>> GetAvailableSlotsAsync(
            int doctorId,
            DateOnly date,
            int durationMinutes,
            DateTime? now = null)
        {
            if (durationMinutes is not (15 or 30 or 45 or 60))
            {
                durationMinutes = DefaultDurationMinutes;
            }

            now ??= DateTime.Now;

            var (periods, _) = await GetEffectiveWorkingPeriodsCoreAsync(doctorId, date);
            if (periods.Count == 0)
            {
                return Array.Empty<TimeSlotDto>();
            }

            var blockedRanges = await GetBookedIntervalsAsync(doctorId, date);

            return ScheduleCalculator.BuildAvailableSlots(periods, durationMinutes, blockedRanges, now.Value);
        }

        public async Task<AvailableSlotsResponseDto> GetAvailableSlotsResponseAsync(
            int doctorId,
            DateOnly date,
            int durationMinutes,
            string doctorName,
            DateTime? now = null)
        {
            if (durationMinutes is not (15 or 30 or 45 or 60))
            {
                durationMinutes = DefaultDurationMinutes;
            }

            now ??= DateTime.Now;

            var (periods, isDayOff) = await GetEffectiveWorkingPeriodsCoreAsync(doctorId, date);

            if (periods.Count == 0)
            {
                var noPeriodMessage = isDayOff
                    ? $"{doctorName} is unavailable on this date."
                    : $"{doctorName} does not work on this date.";
                return new AvailableSlotsResponseDto(Array.Empty<TimeSlotDto>(), null, noPeriodMessage);
            }

            var blockedRanges = await GetBookedIntervalsAsync(doctorId, date);

            var slots = ScheduleCalculator.BuildAvailableSlots(periods, durationMinutes, blockedRanges, now.Value);
            var next = slots.FirstOrDefault(s => s.IsAvailable)?.StartTime;
            var message = BuildNoSlotsMessage(true, isDayOff, doctorName, slots.Count, slots.Count(s => s.IsAvailable));

            return new AvailableSlotsResponseDto(slots, next, message);
        }

        public async Task<DoctorDailyScheduleDto?> GetDailyScheduleAsync(
            int doctorId,
            string doctorName,
            DateOnly date,
            int durationMinutes,
            DateTime? now = null)
        {
            if (durationMinutes is not (15 or 30 or 45 or 60))
            {
                durationMinutes = DefaultDurationMinutes;
            }

            now ??= DateTime.Now;

            if (!await _db.Doctors.AnyAsync(d => d.DoctorId == doctorId))
            {
                return null;
            }

            var (periods, isDayOff) = await GetEffectiveWorkingPeriodsCoreAsync(doctorId, date);

            var appointments = await _db.Appointments
                .Where(a => a.DoctorId == doctorId
                    && a.AppointmentDate == date
                    && a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.StartTime)
                .Select(a => new DailyAppointmentDto(a.StartTime, a.EndTime, a.Patient!.Name, a.Status))
                .ToListAsync();

            var blockedRanges = appointments
                .Select(a => (a.StartTime, a.EndTime))
                .ToList();

            var slots = ScheduleCalculator.BuildAvailableSlots(periods, durationMinutes, blockedRanges, now.Value);
            var next = slots.FirstOrDefault(s => s.IsAvailable)?.StartTime;
            var message = BuildNoSlotsMessage(periods.Count > 0, isDayOff, doctorName, slots.Count, slots.Count(s => s.IsAvailable));

            return new DoctorDailyScheduleDto(
                periods.Count > 0,
                isDayOff,
                periods,
                appointments,
                slots,
                next,
                message);
        }

        public async Task<bool> IsDoctorAvailableAsync(
            int doctorId,
            DateOnly date,
            TimeOnly startTime,
            int durationMinutes)
        {
            var endTime = startTime.AddMinutes(durationMinutes);
            var periods = await GetEffectiveWorkingPeriodsAsync(doctorId, date);
            return periods.Any(p => startTime >= p.StartTime && endTime <= p.EndTime);
        }

        private async Task<(IReadOnlyList<WorkingPeriod> Periods, bool IsDayOff)>
            GetEffectiveWorkingPeriodsCoreAsync(int doctorId, DateOnly date)
        {
            var exception = await _db.ScheduleExceptions
                .FirstOrDefaultAsync(e => e.DoctorId == doctorId && e.ExceptionDate == date);

            var schedules = await _db.DoctorWeeklySchedules
                .Where(s => s.DoctorId == doctorId)
                .ToListAsync();

            var exceptions = exception is null
                ? Array.Empty<ScheduleException>()
                : new[] { exception };

            var periods = ScheduleCalculator.EffectiveWorkingPeriods(date, schedules, exceptions);
            return (periods, exception?.ExceptionType == ScheduleExceptionType.DayOff);
        }

        private async Task<List<(TimeOnly StartTime, TimeOnly EndTime)>> GetBookedIntervalsAsync(int doctorId, DateOnly date)
        {
            var bookedIntervals = await _db.Appointments
                .Where(a => a.DoctorId == doctorId
                    && a.AppointmentDate == date
                    && a.Status != AppointmentStatus.Cancelled)
                .Select(a => new { a.StartTime, a.EndTime })
                .ToListAsync();

            return bookedIntervals
                .Select(a => (a.StartTime, a.EndTime))
                .ToList();
        }

        private static string? BuildNoSlotsMessage(
            bool hasPeriods,
            bool isDayOff,
            string doctorName,
            int candidateCount,
            int availableCount)
        {
            if (!hasPeriods)
            {
                return isDayOff
                    ? $"{doctorName} is unavailable on this date."
                    : $"{doctorName} does not work on this date.";
            }

            if (availableCount > 0)
            {
                return null;
            }

            return candidateCount == 0
                ? "No appointment of the selected duration can fit within the remaining working hours."
                : "No available slots remain for this date.";
        }

        private const int DefaultDurationMinutes = 30;
    }
}
