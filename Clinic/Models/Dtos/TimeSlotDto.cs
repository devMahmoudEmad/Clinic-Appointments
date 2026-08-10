namespace Clinic.Models.Dtos
{
    public record TimeSlotDto(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, bool IsAvailable);
}
