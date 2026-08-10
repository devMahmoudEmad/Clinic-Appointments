namespace Clinic.Models.Dtos
{
    /// <summary>
    /// Payload for the available-slots endpoint. Includes the candidate slots,
    /// the first available start time (if any) and a friendly message explaining
    /// an empty result instead of returning an empty list silently.
    /// </summary>
    public sealed record AvailableSlotsResponseDto(
        IReadOnlyList<TimeSlotDto> Slots,
        TimeOnly? NextAvailableStart,
        string? Message);
}
