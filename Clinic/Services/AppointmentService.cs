using Clinic.Data;
using Clinic.Models.Dtos;
using Clinic.Models.Entities;
using Clinic.Models.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Clinic.Services
{
    public interface IAppointmentService
    {
        Task<PagedResult<AppointmentDto>> GetAppointmentsPagedAsync(
            DateOnly? date,
            int? doctorId,
            string? patientName,
            AppointmentStatus? status,
            int page,
            int pageSize);

        Task<AvailableSlotsResponseDto> GetAvailableSlotsResponseAsync(
            int doctorId,
            DateOnly date,
            int durationMinutes,
            string doctorName);

        Task<AppointmentDto?> GetAppointmentAsync(int appointmentId);

        Task<AppointmentCreationResult> CreateAppointmentAsync(CreateAppointmentCommand command);

        Task<AppointmentCreationResult> UpdateAppointmentAsync(UpdateAppointmentCommand command);

        Task<bool> CancelAppointmentAsync(int appointmentId);

        Task<DoctorDailyScheduleDto?> GetDailyScheduleAsync(int doctorId, string doctorName, DateOnly date, int durationMinutes);
    }

    public class AppointmentService : IAppointmentService
    {
        private readonly ClinicDbContext _db;
        private readonly IScheduleService _scheduleService;
        private readonly ILogger<AppointmentService> _logger;

        public AppointmentService(
            ClinicDbContext db,
            IScheduleService scheduleService,
            ILogger<AppointmentService> logger)
        {
            _db = db;
            _scheduleService = scheduleService;
            _logger = logger;
        }

        public async Task<PagedResult<AppointmentDto>> GetAppointmentsPagedAsync(
            DateOnly? date,
            int? doctorId,
            string? patientName,
            AppointmentStatus? status,
            int page,
            int pageSize)
        {
            var query = _db.Appointments.AsNoTracking();

            if (date.HasValue)
            {
                query = query.Where(a => a.AppointmentDate == date.Value);
            }

            if (doctorId.HasValue)
            {
                query = query.Where(a => a.DoctorId == doctorId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(a => a.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(patientName))
            {
                query = query.Where(a => a.Patient!.Name.Contains(patientName));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AppointmentDto
                {
                    AppointmentId = a.AppointmentId,
                    DoctorId = a.DoctorId,
                    PatientId = a.PatientId,
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Status = a.Status.ToString(),
                    PatientName = a.Patient!.Name,
                    DoctorName = a.Doctor!.Name
                })
                .ToListAsync();

            return PagedResult<AppointmentDto>.Create(items, page, pageSize, totalCount);
        }

        public async Task<AppointmentDto?> GetAppointmentAsync(int appointmentId)
        {
            return await _db.Appointments.AsNoTracking()
                .Where(a => a.AppointmentId == appointmentId)
                .Select(a => new AppointmentDto
                {
                    AppointmentId = a.AppointmentId,
                    DoctorId = a.DoctorId,
                    PatientId = a.PatientId,
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Status = a.Status.ToString(),
                    PatientName = a.Patient!.Name,
                    DoctorName = a.Doctor!.Name
                })
                .FirstOrDefaultAsync();
        }

        public async Task<AvailableSlotsResponseDto> GetAvailableSlotsResponseAsync(
            int doctorId,
            DateOnly date,
            int durationMinutes,
            string doctorName)
        {
            return await _scheduleService.GetAvailableSlotsResponseAsync(doctorId, date, durationMinutes, doctorName);
        }

        public async Task<DoctorDailyScheduleDto?> GetDailyScheduleAsync(
            int doctorId,
            string doctorName,
            DateOnly date,
            int durationMinutes)
        {
            return await _scheduleService.GetDailyScheduleAsync(doctorId, doctorName, date, durationMinutes);
        }

        public async Task<AppointmentCreationResult> CreateAppointmentAsync(CreateAppointmentCommand command)
        {
            if (command.PatientId <= 0)
            {
                return AppointmentCreationResult.Fail("Please select a patient.");
            }

            if (command.DoctorId <= 0)
            {
                return AppointmentCreationResult.Fail("Please select a doctor.");
            }

            if (command.AppointmentDate == default)
            {
                return AppointmentCreationResult.Fail("Please select an appointment date.");
            }

            if (command.AppointmentDate < DateOnly.FromDateTime(DateTime.Today))
            {
                return AppointmentCreationResult.Fail("The appointment date cannot be in the past.");
            }

            if (command.StartTime == default)
            {
                return AppointmentCreationResult.Fail("Please select an appointment time.");
            }

            if (command.DurationMinutes is not (15 or 30 or 45 or 60))
            {
                return AppointmentCreationResult.Fail("The appointment duration is invalid.");
            }

            if (!await _db.Doctors.AnyAsync(d => d.DoctorId == command.DoctorId))
            {
                return AppointmentCreationResult.Fail("The selected doctor does not exist.");
            }

            if (!await _db.Patients.AnyAsync(p => p.PatientId == command.PatientId))
            {
                return AppointmentCreationResult.Fail("The selected patient does not exist.");
            }

            var endTime = command.StartTime.AddMinutes(command.DurationMinutes);

            var workingPeriods = await _scheduleService
                .GetEffectiveWorkingPeriodsAsync(command.DoctorId, command.AppointmentDate);

            if (workingPeriods.Count == 0)
            {
                return AppointmentCreationResult.Fail("The doctor is not available on the selected date.");
            }

            if (!workingPeriods.Any(p => command.StartTime >= p.StartTime && endTime <= p.EndTime))
            {
                return AppointmentCreationResult.Fail("The selected time is outside the doctor's working hours.");
            }

            // Server-side double-booking protection. The overlap check runs inside
            // a Serializable transaction so concurrent requests cannot both see an
            // empty slot and both insert. A unique index on
            // (DoctorId, AppointmentDate, StartTime, Status) is the backstop.
            // The whole transaction runs through the configured execution strategy so
            // transient failures (deadlocks, connection drops) are retried automatically.
            var strategy = _db.Database.CreateExecutionStrategy();

            var result = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable);

                var isOverlapping = await _db.Appointments.AnyAsync(a =>
                    a.DoctorId == command.DoctorId
                    && a.AppointmentDate == command.AppointmentDate
                    && a.Status != AppointmentStatus.Cancelled
                    && a.StartTime < endTime
                    && a.EndTime > command.StartTime);

                if (isOverlapping)
                {
                    return AppointmentCreationResult.Fail(
                        "This appointment slot is no longer available. Please select another time.");
                }

                var appointment = new Appointment
                {
                    DoctorId = command.DoctorId,
                    PatientId = command.PatientId,
                    AppointmentDate = command.AppointmentDate,
                    StartTime = command.StartTime,
                    EndTime = endTime,
                    Status = AppointmentStatus.Scheduled,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Appointments.Add(appointment);

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    _logger.LogWarning(
                        ex,
                        "Concurrent booking rejected for doctor {DoctorId} on {Date} at {Time}",
                        command.DoctorId,
                        command.AppointmentDate,
                        command.StartTime);

                    return AppointmentCreationResult.Fail(
                        "This appointment slot was just booked by someone else. Please select another time.");
                }
                catch (DbUpdateException ex) when (IsDeadlock(ex))
                {
                    _logger.LogWarning(
                        ex,
                        "Deadlock while booking doctor {DoctorId} on {Date} at {Time}; retrying",
                        command.DoctorId,
                        command.AppointmentDate,
                        command.StartTime);

                    _db.Entry(appointment).State = EntityState.Detached;
                    throw;
                }

                await transaction.CommitAsync();
                return AppointmentCreationResult.Ok();
            });

            return result;
        }

        public async Task<AppointmentCreationResult> UpdateAppointmentAsync(UpdateAppointmentCommand command)
        {
            if (command.AppointmentId <= 0)
            {
                return AppointmentCreationResult.Fail("The appointment could not be found.");
            }

            if (command.PatientId <= 0)
            {
                return AppointmentCreationResult.Fail("Please select a patient.");
            }

            if (command.DoctorId <= 0)
            {
                return AppointmentCreationResult.Fail("Please select a doctor.");
            }

            if (command.AppointmentDate == default)
            {
                return AppointmentCreationResult.Fail("Please select an appointment date.");
            }

            if (command.AppointmentDate < DateOnly.FromDateTime(DateTime.Today))
            {
                return AppointmentCreationResult.Fail("The appointment date cannot be in the past.");
            }

            if (command.StartTime == default)
            {
                return AppointmentCreationResult.Fail("Please select an appointment time.");
            }

            if (command.DurationMinutes is not (15 or 30 or 45 or 60))
            {
                return AppointmentCreationResult.Fail("The appointment duration is invalid.");
            }

            // Same double-booking protection as creation: the overlap check runs
            // inside a Serializable transaction (excluding this appointment), with
            // the unique index (DoctorId, AppointmentDate, StartTime, Status) as a
            // backstop. The execution strategy retries transient failures only.
            var strategy = _db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable);

                var appointment = await _db.Appointments
                    .FirstOrDefaultAsync(a => a.AppointmentId == command.AppointmentId);

                if (appointment is null)
                {
                    return AppointmentCreationResult.Fail("The appointment no longer exists.");
                }

                if (appointment.Status == AppointmentStatus.Cancelled)
                {
                    return AppointmentCreationResult.Fail("A cancelled appointment cannot be edited.");
                }

                if (!await _db.Doctors.AnyAsync(d => d.DoctorId == command.DoctorId))
                {
                    return AppointmentCreationResult.Fail("The selected doctor does not exist.");
                }

                if (!await _db.Patients.AnyAsync(p => p.PatientId == command.PatientId))
                {
                    return AppointmentCreationResult.Fail("The selected patient does not exist.");
                }

                var endTime = command.StartTime.AddMinutes(command.DurationMinutes);

                var workingPeriods = await _scheduleService
                    .GetEffectiveWorkingPeriodsAsync(command.DoctorId, command.AppointmentDate);

                if (workingPeriods.Count == 0)
                {
                    return AppointmentCreationResult.Fail("The doctor is not available on the selected date.");
                }

                if (!workingPeriods.Any(p => command.StartTime >= p.StartTime && endTime <= p.EndTime))
                {
                    return AppointmentCreationResult.Fail("The selected time is outside the doctor's working hours.");
                }

                var isOverlapping = await _db.Appointments.AnyAsync(a =>
                    a.AppointmentId != command.AppointmentId
                    && a.DoctorId == command.DoctorId
                    && a.AppointmentDate == command.AppointmentDate
                    && a.Status != AppointmentStatus.Cancelled
                    && a.StartTime < endTime
                    && a.EndTime > command.StartTime);

                if (isOverlapping)
                {
                    return AppointmentCreationResult.Fail(
                        "This appointment slot is no longer available. Please select another time.");
                }

                appointment.DoctorId = command.DoctorId;
                appointment.PatientId = command.PatientId;
                appointment.AppointmentDate = command.AppointmentDate;
                appointment.StartTime = command.StartTime;
                appointment.EndTime = endTime;

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    _logger.LogWarning(
                        ex,
                        "Concurrent booking rejected while updating appointment {AppointmentId}",
                        command.AppointmentId);

                    return AppointmentCreationResult.Fail(
                        "This appointment slot was just booked by someone else. Please select another time.");
                }
                catch (DbUpdateException ex) when (IsDeadlock(ex))
                {
                    _logger.LogWarning(
                        ex,
                        "Deadlock while updating appointment {AppointmentId}; retrying",
                        command.AppointmentId);

                    _db.Entry(appointment).State = EntityState.Detached;
                    throw;
                }

                await transaction.CommitAsync();
                return AppointmentCreationResult.Ok();
            });
        }

        public async Task<bool> CancelAppointmentAsync(int appointmentId)
        {
            var appointment = await _db.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment is null || appointment.Status == AppointmentStatus.Cancelled)
            {
                return false;
            }

            appointment.Status = AppointmentStatus.Cancelled;
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Appointment {AppointmentId} for doctor {DoctorId} on {Date} at {Time} was cancelled",
                appointment.AppointmentId,
                appointment.DoctorId,
                appointment.AppointmentDate,
                appointment.StartTime);

            return true;
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            if (ex.InnerException is SqlException sqlEx)
            {
                return sqlEx.Number is 2601 or 2627;
            }

            var message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase)
                || message.Contains("UNIQUE index", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDeadlock(DbUpdateException ex)
        {
            if (ex.InnerException is SqlException sqlEx)
            {
                return sqlEx.Number == 1205;
            }

            var message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("deadlock", StringComparison.OrdinalIgnoreCase)
                || message.Contains("was deadlocked", StringComparison.OrdinalIgnoreCase);
        }
    }
}
