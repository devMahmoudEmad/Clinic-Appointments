using System.ComponentModel.DataAnnotations;

namespace Clinic.ViewModels.Appointments
{
    /// <summary>
    /// Only the durations offered in the appointment form (15, 30, 45, 60
    /// minutes) are accepted. Anything else is rejected server-side even though
    /// the UI only renders those options.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class AllowedAppointmentDurationsAttribute : ValidationAttribute
    {
        public const int FifteenMinutes = 15;

        public const int ThirtyMinutes = 30;

        public const int FortyFiveMinutes = 45;

        public const int SixtyMinutes = 60;

        private static readonly int[] AllowedDurations = { FifteenMinutes, ThirtyMinutes, FortyFiveMinutes, SixtyMinutes };

        public AllowedAppointmentDurationsAttribute()
            : base("Please select a valid appointment duration.")
        {
        }

        public override bool IsValid(object? value)
        {
            return value is int duration && AllowedDurations.Contains(duration);
        }
    }
}
