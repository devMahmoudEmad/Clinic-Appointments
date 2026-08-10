using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Clinic.Validation
{
    /// <summary>
    /// Replaces technical model-binding error messages (JSON deserialization
    /// details, type names, "Path: $.X", line/byte positions) with short,
    /// user-friendly messages. Applied at the controller boundary after binding
    /// and before the ModelState.IsValid check. The original exception is still
    /// logged by ASP.NET Core's model binding pipeline; it is never rendered.
    /// </summary>
    public static class ModelStateErrorMapper
    {
        public static void ReplaceTechnicalErrors(ModelStateDictionary modelState)
        {
            foreach (var key in modelState.Keys.ToList())
            {
                var entry = modelState[key]!;
                if (entry.Errors.Count == 0)
                {
                    continue;
                }

                var original = entry.Errors.ToList();
                entry.Errors.Clear();

                foreach (var error in original)
                {
                    var raw = error.Exception?.Message ?? error.ErrorMessage;

                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        entry.Errors.Add(error);
                        continue;
                    }

                    if (LooksTechnical(raw))
                    {
                        var propertyName = NormalizeKey(PropertyName(key, raw));
                        entry.Errors.Add(new ModelError(FriendlyMessage(propertyName, raw)));
                    }
                    else
                    {
                        entry.Errors.Add(error);
                    }
                }
            }
        }

        /// <summary>
        /// Maps a ModelState key (e.g. "viewModel.Gender", "$.Gender" or "Gender")
        /// to its plain property name for structured AJAX error responses.
        /// </summary>
        public static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            var name = key;
            var lastDot = name.LastIndexOf('.');
            if (lastDot >= 0)
            {
                name = name[(lastDot + 1)..];
            }

            return name.TrimStart('$', '.');
        }

        private static bool LooksTechnical(string message)
        {
            return message.Contains("could not be converted", StringComparison.OrdinalIgnoreCase)
                || message.Contains("JSON value", StringComparison.OrdinalIgnoreCase)
                || message.Contains("System.Nullable", StringComparison.OrdinalIgnoreCase)
                || message.Contains("System.String", StringComparison.OrdinalIgnoreCase)
                || message.Contains("System.Int", StringComparison.OrdinalIgnoreCase)
                || message.Contains("System.DateOnly", StringComparison.OrdinalIgnoreCase)
                || message.Contains("System.TimeOnly", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Path: $", StringComparison.OrdinalIgnoreCase)
                || message.Contains("LineNumber", StringComparison.OrdinalIgnoreCase)
                || message.Contains("BytePositionInLine", StringComparison.OrdinalIgnoreCase)
                || message.Contains("InvalidCastException", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Failed to bind", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("The value '", StringComparison.OrdinalIgnoreCase)
                || message.Contains(" is not valid for ", StringComparison.OrdinalIgnoreCase);
        }

        private static string PropertyName(string key, string rawMessage)
        {
            // JSON deserialization errors carry the offending path in the message,
            // e.g. "Path: $.Gender | LineNumber: ...". Prefer that over the key.
            const string pathMarker = "Path: $.";
            var pathIndex = rawMessage.IndexOf(pathMarker, StringComparison.Ordinal);

            if (pathIndex >= 0)
            {
                var remaining = rawMessage[(pathIndex + pathMarker.Length)..];
                var end = remaining.IndexOfAny(new[] { ' ', '|', '\r', '\n' });
                var segment = end >= 0 ? remaining[..end] : remaining;
                if (segment.Length > 0)
                {
                    return segment;
                }
            }

            return key;
        }

        private static string FriendlyMessage(string propertyName, string rawMessage)
        {
            var isEmpty = rawMessage.Contains("''");

            return propertyName switch
            {
                "Gender" => "Please select a valid gender.",
                "BirthDate" => isEmpty
                    ? "Please select a birth date."
                    : "Please enter a valid birth date.",
                "AppointmentDate" => isEmpty
                    ? "Please select an appointment date."
                    : "Please enter a valid date.",
                "ExceptionDate" => isEmpty
                    ? "Please select a date."
                    : "Please enter a valid date.",
                "Date" => "Please enter a valid date.",
                "StartTime" or "EndTime" or "StartTimeException" or "EndTimeException" =>
                    "Please enter a valid time.",
                "DoctorId" => isEmpty
                    ? "Please select a doctor."
                    : "Please enter a valid value.",
                "PatientId" => isEmpty
                    ? "Please select a patient."
                    : "Please enter a valid value.",
                "ClinicId" => isEmpty
                    ? "Please select a clinic."
                    : "Please enter a valid value.",
                "DurationMinutes" => "Please select a valid appointment duration.",
                _ => "Please enter a valid value."
            };
        }
    }
}
