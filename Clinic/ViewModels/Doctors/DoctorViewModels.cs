using Clinic.Models.Dtos;
using Clinic.Models.Entities;
using Clinic.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Clinic.ViewModels.Doctors
{
    public class DoctorIndexViewModel : IPagedViewModel
    {
        public IReadOnlyList<DoctorDto> Doctors { get; set; } = new List<DoctorDto>();

        public int CurrentPage { get; set; } = 1;

        public int TotalPages { get; set; }
    }

    public class DoctorCreateViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Please select a clinic.")]
        public int ClinicId { get; set; }

        [Required(ErrorMessage = "Doctor name is required.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Specialization is required.")]
        [StringLength(100)]
        public string Specialization { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required.")]
        [StringLength(50)]
        public string Phone { get; set; } = string.Empty;

        public List<SelectListItem> Clinics { get; set; } = new();
    }

    public class DoctorEditViewModel
    {
        public int DoctorId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select a clinic.")]
        public int ClinicId { get; set; }

        [Required(ErrorMessage = "Doctor name is required.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Specialization is required.")]
        [StringLength(100)]
        public string Specialization { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required.")]
        [StringLength(50)]
        public string Phone { get; set; } = string.Empty;

        public List<SelectListItem> Clinics { get; set; } = new();
    }

    public class DoctorDetailsViewModel
    {
        public DoctorDto Doctor { get; set; } = new();

        public List<DoctorWeeklySchedule> WeeklySchedules { get; set; } = new();

        public List<ScheduleException> ScheduleExceptions { get; set; } = new();

        public string ScheduleSummary
        {
            get
            {
                var active = WeeklySchedules.Where(s => s.IsActive).ToList();

                if (active.Count == 0)
                {
                    return "No active weekly schedule.";
                }

                var parts = active
                    .GroupBy(s => (s.StartTime, s.EndTime))
                    .OrderBy(g => g.Key.StartTime)
                    .ThenBy(g => g.Key.EndTime)
                    .Select(g => $"{FormatDayRanges(g.Select(s => s.DayOfWeek))} {g.Key.StartTime:HH\\:mm}–{g.Key.EndTime:HH\\:mm}");

                return string.Join("; ", parts);
            }
        }

        private static string FormatDayRanges(IEnumerable<DayOfWeek> days)
        {
            var dayOrder = new[]
            {
                DayOfWeek.Saturday,
                DayOfWeek.Sunday,
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            };

            var indexes = days
                .Select(d => Array.IndexOf(dayOrder, d))
                .OrderBy(i => i)
                .Distinct()
                .ToList();

            var ranges = new List<string>();
            var rangeStart = indexes[0];
            var prev = indexes[0];

            for (var i = 1; i < indexes.Count; i++)
            {
                if (indexes[i] == prev + 1)
                {
                    prev = indexes[i];
                    continue;
                }

                ranges.Add(FormatDayRange(rangeStart, prev, dayOrder));
                rangeStart = prev = indexes[i];
            }

            ranges.Add(FormatDayRange(rangeStart, prev, dayOrder));
            return string.Join(", ", ranges);
        }

        private static string FormatDayRange(int first, int last, DayOfWeek[] dayOrder)
        {
            if (first == last)
            {
                return DayShort(dayOrder[first]);
            }

            return DayShort(dayOrder[first]) + "–" + DayShort(dayOrder[last]);
        }

        private static string DayShort(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Saturday => "Sat",
                DayOfWeek.Sunday => "Sun",
                DayOfWeek.Monday => "Mon",
                DayOfWeek.Tuesday => "Tue",
                DayOfWeek.Wednesday => "Wed",
                DayOfWeek.Thursday => "Thu",
                _ => "Fri"
            };
        }

        // Weekly schedule add form
        public DayOfWeek DayOfWeek { get; set; }

        public TimeOnly? StartTime { get; set; }

        public TimeOnly? EndTime { get; set; }

        // Exception add form
        [DataType(DataType.Date)]
        public DateOnly ExceptionDate { get; set; }

        public ScheduleExceptionType ExceptionType { get; set; }

        public TimeOnly? StartTimeException { get; set; }

        public TimeOnly? EndTimeException { get; set; }

        public string? Reason { get; set; }
    }
}
