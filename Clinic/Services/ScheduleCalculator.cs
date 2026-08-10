using Clinic.Models.Dtos;
using Clinic.Models.Entities;
using Clinic.Models.Enums;

namespace Clinic.Services
{
    /// <summary>
    /// Pure scheduling rules, free of any database dependency so the logic is
    /// directly unit-testable. All time/overlap rules live here and are reused
    /// by <see cref="ScheduleService"/>.
    /// </summary>
    public static class ScheduleCalculator
    {
        /// <summary>
        /// Effective working periods for a doctor/date.
        /// 1. A DayOff exception yields no working period.
        /// 2. A ModifiedHours exception overrides the weekly schedule.
        /// 3. Otherwise the active weekly schedule row(s) for that day are used.
        /// </summary>
        public static IReadOnlyList<WorkingPeriod> EffectiveWorkingPeriods(
            DateOnly date,
            IEnumerable<DoctorWeeklySchedule> weeklySchedules,
            IEnumerable<ScheduleException> exceptions)
        {
            var exception = exceptions.FirstOrDefault();
            if (exception?.ExceptionType == ScheduleExceptionType.DayOff)
            {
                return Array.Empty<WorkingPeriod>();
            }

            if (exception?.ExceptionType == ScheduleExceptionType.ModifiedHours)
            {
                return new[]
                {
                    new WorkingPeriod(date, exception.StartTime!.Value, exception.EndTime!.Value)
                };
            }

            return weeklySchedules
                .Where(s => s.IsActive && s.DayOfWeek == date.DayOfWeek)
                .Select(s => new WorkingPeriod(date, s.StartTime, s.EndTime))
                .OrderBy(p => p.StartTime)
                .ToList();
        }

        /// <summary>
        /// Generates candidate slots on a fixed 15-minute start grid regardless
        /// of <paramref name="durationMinutes"/>: the duration only determines
        /// the end time, and a candidate is only valid when the whole
        /// <c>start + duration</c> fits inside a single working period.
        ///
        /// When <paramref name="now"/> falls on the same date as a working
        /// period, slots that would already have started are skipped and the
        /// grid is aligned so the first candidate is the first 15-minute
        /// boundary at or after the current time.
        ///
        /// Each candidate is marked against the given blocked (already booked)
        /// intervals using the overlap rule.
        /// </summary>
        public static IReadOnlyList<TimeSlotDto> BuildAvailableSlots(
            IReadOnlyList<WorkingPeriod> periods,
            int durationMinutes,
            IReadOnlyList<(TimeOnly StartTime, TimeOnly EndTime)> blockedRanges,
            DateTime? now = null)
        {
            var slots = new List<TimeSlotDto>();

            foreach (var period in periods)
            {
                var cursor = CeilToGrid(period.StartTime);
                while (cursor.AddMinutes(durationMinutes) <= period.EndTime)
                {
                    var slotEnd = cursor.AddMinutes(durationMinutes);
                    var available = !blockedRanges.Any(b =>
                        Overlaps(cursor, slotEnd, b.StartTime, b.EndTime));

                    slots.Add(new TimeSlotDto(period.Date, cursor, slotEnd, available));
                    cursor = cursor.AddMinutes(GridMinutes);
                }
            }

            if (now is not null)
            {
                var today = DateOnly.FromDateTime(now.Value);
                var currentTime = TimeOnly.FromDateTime(now.Value);

                slots = slots
                    .Where(s => s.Date != today || s.StartTime >= currentTime)
                    .ToList();
            }

            return slots;
        }

        /// <summary>
        /// First available start time for the given working periods and booked
        /// intervals, or <see langword="null"/> when nothing is available.
        /// </summary>
        public static TimeOnly? NextAvailableStart(
            IReadOnlyList<WorkingPeriod> periods,
            int durationMinutes,
            IReadOnlyList<(TimeOnly StartTime, TimeOnly EndTime)> blockedRanges,
            DateTime now)
        {
            return BuildAvailableSlots(periods, durationMinutes, blockedRanges, now)
                .FirstOrDefault(s => s.IsAvailable)?.StartTime;
        }

        /// <summary>
        /// Rounds a time up to the next 15-minute wall-clock boundary (16:07 → 16:15).
        /// </summary>
        private static TimeOnly CeilToGrid(TimeOnly time)
        {
            var totalMinutes = time.Hour * 60 + time.Minute;
            var remainder = totalMinutes % GridMinutes;
            if (remainder == 0)
            {
                return time;
            }

            var next = totalMinutes + (GridMinutes - remainder);
            return new TimeOnly(next / 60, next % 60);
        }

        /// <summary>
        /// Overlap rule: two intervals overlap when one starts before the other
        /// ends and ends after the other starts.
        /// </summary>
        public static bool Overlaps(TimeOnly startA, TimeOnly endA, TimeOnly startB, TimeOnly endB)
            => startA < endB && endA > startB;

        private const int GridMinutes = 15;
    }
}
