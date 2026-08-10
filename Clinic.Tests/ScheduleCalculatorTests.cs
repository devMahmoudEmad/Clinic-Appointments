using Clinic.Models.Dtos;
using Clinic.Models.Entities;
using Clinic.Models.Enums;
using Clinic.Services;

namespace Clinic.Tests
{
    public class ScheduleCalculatorTests
    {
        private static DateOnly Date(int year, int month, int day) => new(year, month, day);

        private static DoctorWeeklySchedule Weekly(DayOfWeek day, int startHour, int endHour)
        {
            return new DoctorWeeklySchedule
            {
                DayOfWeek = day,
                StartTime = new TimeOnly(startHour, 0),
                EndTime = new TimeOnly(endHour, 0),
                IsActive = true
            };
        }

        private static ScheduleException DayOff() => new()
        {
            ExceptionType = ScheduleExceptionType.DayOff
        };

        private static ScheduleException ModifiedHours(int startHour, int endHour) => new()
        {
            ExceptionType = ScheduleExceptionType.ModifiedHours,
            StartTime = new TimeOnly(startHour, 0),
            EndTime = new TimeOnly(endHour, 0)
        };

        [Fact]
        public void EffectiveWorkingPeriods_DoctorWorksSaturday_ReturnsPeriod()
        {
            var schedules = new[] { Weekly(DayOfWeek.Saturday, 16, 20) };
            var date = Date(2026, 8, 15); // Saturday

            var periods = ScheduleCalculator.EffectiveWorkingPeriods(date, schedules, Array.Empty<ScheduleException>());

            var period = Assert.Single(periods);
            Assert.Equal(new TimeOnly(16, 0), period.StartTime);
            Assert.Equal(new TimeOnly(20, 0), period.EndTime);
        }

        [Fact]
        public void EffectiveWorkingPeriods_DoctorDoesNotWorkFriday_ReturnsNoPeriod()
        {
            var schedules = new[] { Weekly(DayOfWeek.Saturday, 16, 20) };
            var date = Date(2026, 8, 14); // Friday

            var periods = ScheduleCalculator.EffectiveWorkingPeriods(date, schedules, Array.Empty<ScheduleException>());

            Assert.Empty(periods);
        }

        [Fact]
        public void EffectiveWorkingPeriods_DayOffException_ReturnsNoPeriod()
        {
            var schedules = new[] { Weekly(DayOfWeek.Saturday, 16, 20) };
            var date = Date(2026, 8, 15); // Saturday

            var periods = ScheduleCalculator.EffectiveWorkingPeriods(date, schedules, new[] { DayOff() });

            Assert.Empty(periods);
        }

        [Fact]
        public void EffectiveWorkingPeriods_ModifiedHoursException_OverridesWeeklySchedule()
        {
            var schedules = new[] { Weekly(DayOfWeek.Saturday, 16, 20) };
            var date = Date(2026, 8, 15); // Saturday

            var periods = ScheduleCalculator.EffectiveWorkingPeriods(date, schedules, new[] { ModifiedHours(18, 22) });

            var period = Assert.Single(periods);
            Assert.Equal(new TimeOnly(18, 0), period.StartTime);
            Assert.Equal(new TimeOnly(22, 0), period.EndTime);
        }

        [Fact]
        public void EffectiveWorkingPeriods_InactiveScheduleRow_IsIgnored()
        {
            var schedule = Weekly(DayOfWeek.Saturday, 16, 20);
            schedule.IsActive = false;
            var date = Date(2026, 8, 15); // Saturday

            var periods = ScheduleCalculator.EffectiveWorkingPeriods(date, new[] { schedule }, Array.Empty<ScheduleException>());

            Assert.Empty(periods);
        }

        [Fact]
        public void BuildAvailableSlots_ThirtyMinutes_StartsOnFifteenMinuteGrid()
        {
            var periods = new[]
            {
                new WorkingPeriod(Date(2026, 8, 15), new TimeOnly(16, 0), new TimeOnly(20, 0))
            };

            var slots = ScheduleCalculator.BuildAvailableSlots(periods, 30, Array.Empty<(TimeOnly, TimeOnly)>());

            Assert.Equal(15, slots.Count);
            Assert.Equal(new TimeOnly(16, 0), slots[0].StartTime);
            Assert.Equal(new TimeOnly(16, 30), slots[0].EndTime);
            Assert.Equal(new TimeOnly(16, 15), slots[1].StartTime);
            Assert.Equal(new TimeOnly(19, 30), slots[^1].StartTime);
            Assert.Equal(new TimeOnly(20, 0), slots[^1].EndTime);
            Assert.All(slots, slot => Assert.True(slot.IsAvailable));
        }

        [Fact]
        public void BuildAvailableSlots_SixtyMinutes_LastStartFitsWithinWorkingHours()
        {
            var periods = new[]
            {
                new WorkingPeriod(Date(2026, 8, 15), new TimeOnly(16, 0), new TimeOnly(20, 0))
            };

            var slots = ScheduleCalculator.BuildAvailableSlots(periods, 60, Array.Empty<(TimeOnly, TimeOnly)>());

            Assert.Equal(13, slots.Count);
            Assert.Contains(slots, s => s.StartTime == new TimeOnly(19, 0));   // 19:00 + 60 fits exactly
            Assert.DoesNotContain(slots, s => s.StartTime == new TimeOnly(19, 15)); // 19:15 + 60 > 20:00
        }

        [Fact]
        public void BuildAvailableSlots_CurrentTime_SkipsElapsedAndAlignsToGrid()
        {
            var periods = new[]
            {
                new WorkingPeriod(Date(2026, 8, 15), new TimeOnly(16, 0), new TimeOnly(20, 0))
            };
            var now = new DateTime(2026, 8, 15, 17, 12, 0);

            var slots = ScheduleCalculator.BuildAvailableSlots(periods, 30, Array.Empty<(TimeOnly, TimeOnly)>(), now);

            Assert.Equal(new TimeOnly(17, 15), slots[0].StartTime);
            Assert.All(slots, slot => Assert.True(slot.StartTime >= new TimeOnly(17, 12)));
        }

        [Fact]
        public void BuildAvailableSlots_CurrentTimeExactlyOnGridStart_KeepsIt()
        {
            var periods = new[]
            {
                new WorkingPeriod(Date(2026, 8, 15), new TimeOnly(16, 0), new TimeOnly(20, 0))
            };
            var now = new DateTime(2026, 8, 15, 17, 0, 0);

            var slots = ScheduleCalculator.BuildAvailableSlots(periods, 30, Array.Empty<(TimeOnly, TimeOnly)>(), now);

            Assert.Equal(new TimeOnly(17, 0), slots[0].StartTime);
        }

        [Fact]
        public void BuildAvailableSlots_CurrentTimeAfterWorkingHours_ReturnsNoSlots()
        {
            var periods = new[]
            {
                new WorkingPeriod(Date(2026, 8, 15), new TimeOnly(16, 0), new TimeOnly(20, 0))
            };
            var now = new DateTime(2026, 8, 15, 23, 50, 0);

            var slots = ScheduleCalculator.BuildAvailableSlots(periods, 30, Array.Empty<(TimeOnly, TimeOnly)>(), now);

            Assert.Empty(slots);
        }

        [Fact]
        public void BuildAvailableSlots_BookedInterval_MarksSlotUnavailable()
        {
            var periods = new[]
            {
                new WorkingPeriod(Date(2026, 8, 15), new TimeOnly(16, 0), new TimeOnly(20, 0))
            };

            var blocked = new[]
            {
                (new TimeOnly(16, 30), new TimeOnly(17, 0))
            };

            var slots = ScheduleCalculator.BuildAvailableSlots(periods, 30, blocked);

            var bookedSlot = Assert.Single(slots, s => s.StartTime == new TimeOnly(16, 30));
            Assert.False(bookedSlot.IsAvailable);

            var freeSlot = Assert.Single(slots, s => s.StartTime == new TimeOnly(16, 0));
            Assert.True(freeSlot.IsAvailable);
        }

        [Fact]
        public void BuildAvailableSlots_AdjacentBookedInterval_TouchingStartIsAvailable()
        {
            var periods = new[]
            {
                new WorkingPeriod(Date(2026, 8, 15), new TimeOnly(16, 0), new TimeOnly(20, 0))
            };
            var blocked = new[]
            {
                (new TimeOnly(17, 0), new TimeOnly(17, 30))
            };

            var slots = ScheduleCalculator.BuildAvailableSlots(periods, 30, blocked);

            var adjacent = Assert.Single(slots, s => s.StartTime == new TimeOnly(17, 30));
            Assert.True(adjacent.IsAvailable);
        }

        [Fact]
        public void BuildAvailableSlots_AllBlocked_NoAvailableSlots()
        {
            var periods = new[]
            {
                new WorkingPeriod(Date(2026, 8, 15), new TimeOnly(16, 0), new TimeOnly(20, 0))
            };
            var blocked = new[]
            {
                (new TimeOnly(16, 0), new TimeOnly(20, 0))
            };

            var slots = ScheduleCalculator.BuildAvailableSlots(periods, 30, blocked);

            Assert.Equal(15, slots.Count);
            Assert.DoesNotContain(slots, s => s.IsAvailable);
        }

        [Fact]
        public void BuildAvailableSlots_MultiplePeriods_GeneratesSlotsForEachWithoutGaps()
        {
            var periods = new[]
            {
                new WorkingPeriod(Date(2026, 8, 15), new TimeOnly(9, 0), new TimeOnly(10, 0)),
                new WorkingPeriod(Date(2026, 8, 15), new TimeOnly(17, 0), new TimeOnly(18, 0))
            };

            var slots = ScheduleCalculator.BuildAvailableSlots(periods, 30, Array.Empty<(TimeOnly, TimeOnly)>());

            Assert.Equal(6, slots.Count);
            Assert.Equal(new TimeOnly(9, 0), slots[0].StartTime);
            Assert.Equal(new TimeOnly(9, 30), slots[2].StartTime);
            Assert.Equal(new TimeOnly(17, 0), slots[3].StartTime);
            Assert.Equal(new TimeOnly(17, 30), slots[^1].StartTime);
            Assert.DoesNotContain(slots, s => s.StartTime >= new TimeOnly(10, 0) && s.StartTime < new TimeOnly(17, 0));
        }

        [Fact]
        public void NextAvailableStart_ReturnsFirstAvailableStart()
        {
            var periods = new[]
            {
                new WorkingPeriod(Date(2026, 8, 15), new TimeOnly(16, 0), new TimeOnly(20, 0))
            };
            var blocked = new[]
            {
                (new TimeOnly(16, 0), new TimeOnly(17, 0))
            };

            var next = ScheduleCalculator.NextAvailableStart(periods, 30, blocked, new DateTime(2026, 8, 15, 16, 0, 0));

            Assert.Equal(new TimeOnly(17, 0), next);
        }

        [Fact]
        public void NextAvailableStart_AllBlocked_ReturnsNull()
        {
            var periods = new[]
            {
                new WorkingPeriod(Date(2026, 8, 15), new TimeOnly(16, 0), new TimeOnly(20, 0))
            };
            var blocked = new[]
            {
                (new TimeOnly(16, 0), new TimeOnly(20, 0))
            };

            var next = ScheduleCalculator.NextAvailableStart(periods, 30, blocked, new DateTime(2026, 8, 15, 16, 0, 0));

            Assert.Null(next);
        }

        [Theory]
        [InlineData(16, 0, 16, 30, 16, 15, 16, 45, true)]  // overlapping
        [InlineData(16, 0, 17, 0, 16, 30, 17, 30, true)]   // overlapping
        [InlineData(16, 0, 16, 30, 16, 30, 17, 0, false)]  // adjacent, touching edge only
        [InlineData(16, 0, 16, 30, 16, 30, 16, 45, false)] // adjacent, touching edge only
        [InlineData(10, 0, 11, 0, 12, 0, 13, 0, false)]    // disjoint
        public void Overlaps_FollowsStandardRule(
            int s1h, int s1m, int e1h, int e1m,
            int s2h, int s2m, int e2h, int e2m,
            bool expected)
        {
            var startA = new TimeOnly(s1h, s1m);
            var endA = new TimeOnly(e1h, e1m);
            var startB = new TimeOnly(s2h, s2m);
            var endB = new TimeOnly(e2h, e2m);

            Assert.Equal(expected, ScheduleCalculator.Overlaps(startA, endA, startB, endB));
        }
    }
}
