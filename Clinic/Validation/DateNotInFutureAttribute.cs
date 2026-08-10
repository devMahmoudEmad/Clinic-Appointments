using System.ComponentModel.DataAnnotations;

namespace Clinic.Validation
{
    /// <summary>
    /// Rejects a date that is in the future (e.g. a patient birth date).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class DateNotInFutureAttribute : ValidationAttribute
    {
        public DateNotInFutureAttribute()
            : base("Birth date cannot be in the future.")
        {
        }

        public override bool IsValid(object? value)
        {
            return value is not DateOnly date
                || date <= DateOnly.FromDateTime(DateTime.Today);
        }
    }
}
