using Clinic.Data;
using Clinic.Models.Dtos;
using Clinic.Models.Entities;
using Clinic.Models.Enums;
using Clinic.Services;
using ClinicEntity = Clinic.Models.Entities.Clinic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clinic.Tests
{
    public class SchedulingServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ClinicDbContext _db;
        private readonly ScheduleService _scheduleService;
        private readonly AppointmentService _appointmentService;
        private readonly DoctorService _doctorService;
        private readonly PatientService _patientService;
        private readonly int _doctorId;
        private readonly int _patientId;

        private static readonly DateOnly Saturday = new(2026, 8, 15);
        private static readonly DateOnly Friday = new(2026, 8, 14);

        public SchedulingServiceTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ClinicDbContext>()
                .UseSqlite(_connection)
                .Options;

            _db = new ClinicDbContext(options);
            _db.Database.EnsureCreated();

            var clinic = new ClinicEntity { Name = "Test Clinic" };
            _db.Clinics.Add(clinic);
            _db.SaveChanges();

            var doctor = new Doctor
            {
                ClinicId = clinic.ClinicId,
                Name = "Dr. Test",
                Specialization = "General Medicine",
                Phone = "0100"
            };
            _db.Doctors.Add(doctor);
            _db.SaveChanges();

            foreach (var day in new[]
            {
                DayOfWeek.Saturday,
                DayOfWeek.Sunday,
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday
            })
            {
                _db.DoctorWeeklySchedules.Add(new DoctorWeeklySchedule
                {
                    DoctorId = doctor.DoctorId,
                    DayOfWeek = day,
                    StartTime = new TimeOnly(16, 0),
                    EndTime = new TimeOnly(20, 0),
                    IsActive = true
                });
            }

            _db.Patients.Add(new Patient
            {
                Name = "Patient One",
                BirthDate = new DateOnly(1990, 1, 1),
                Gender = Gender.Male,
                Phone = "0110"
            });
            _db.SaveChanges();

            _doctorId = doctor.DoctorId;
            _patientId = _db.Patients.Single().PatientId;

            _scheduleService = new ScheduleService(_db);
            _doctorService = new DoctorService(_db);
            _patientService = new PatientService(_db);
            _appointmentService = new AppointmentService(
                _db,
                _scheduleService,
                NullLogger<AppointmentService>.Instance);
        }

        [Fact]
        public async Task GetEffectiveWorkingPeriods_DoctorWorksSaturday_ReturnsPeriod()
        {
            var periods = await _scheduleService.GetEffectiveWorkingPeriodsAsync(_doctorId, Saturday);

            var period = Assert.Single(periods);
            Assert.Equal(new TimeOnly(16, 0), period.StartTime);
            Assert.Equal(new TimeOnly(20, 0), period.EndTime);
        }

        [Fact]
        public async Task GetEffectiveWorkingPeriods_FridayOff_ReturnsNoPeriod()
        {
            var periods = await _scheduleService.GetEffectiveWorkingPeriodsAsync(_doctorId, Friday);

            Assert.Empty(periods);
        }

        [Fact]
        public async Task GetEffectiveWorkingPeriods_UnknownDoctor_ReturnsNoPeriod()
        {
            var periods = await _scheduleService.GetEffectiveWorkingPeriodsAsync(9999, Saturday);

            Assert.Empty(periods);
        }

        [Fact]
        public async Task GetEffectiveWorkingPeriods_DayOffException_ReturnsNoPeriod()
        {
            var added = await _doctorService.AddScheduleExceptionAsync(
                _doctorId,
                Saturday,
                ScheduleExceptionType.DayOff,
                null,
                null,
                "Vacation");
            Assert.True(added);

            var periods = await _scheduleService.GetEffectiveWorkingPeriodsAsync(_doctorId, Saturday);

            Assert.Empty(periods);
        }

        [Fact]
        public async Task GetEffectiveWorkingPeriods_ModifiedHoursException_OverridesWeeklySchedule()
        {
            var added = await _doctorService.AddScheduleExceptionAsync(
                _doctorId,
                Saturday,
                ScheduleExceptionType.ModifiedHours,
                new TimeOnly(18, 0),
                new TimeOnly(22, 0),
                "Evening shift");
            Assert.True(added);

            var periods = await _scheduleService.GetEffectiveWorkingPeriodsAsync(_doctorId, Saturday);

            var period = Assert.Single(periods);
            Assert.Equal(new TimeOnly(18, 0), period.StartTime);
            Assert.Equal(new TimeOnly(22, 0), period.EndTime);
        }

        [Fact]
        public async Task AddScheduleException_DoesNotModifyWeeklySchedule()
        {
            await _doctorService.AddScheduleExceptionAsync(
                _doctorId,
                Saturday,
                ScheduleExceptionType.ModifiedHours,
                new TimeOnly(18, 0),
                new TimeOnly(22, 0),
                null);

            var schedule = await _doctorService.GetWeeklyScheduleAsync(_doctorId);
            var saturdayRows = schedule.Where(s => s.DayOfWeek == DayOfWeek.Saturday);

            var row = Assert.Single(saturdayRows);
            Assert.Equal(new TimeOnly(16, 0), row.StartTime);
            Assert.Equal(new TimeOnly(20, 0), row.EndTime);
        }

        [Fact]
        public async Task GetAvailableSlots_SixteenToTwenty_ReturnsFifteenAvailableSlots()
        {
            var slots = await _scheduleService.GetAvailableSlotsAsync(_doctorId, Saturday, 30);

            Assert.Equal(15, slots.Count);
            Assert.Equal(new TimeOnly(16, 0), slots[0].StartTime);
            Assert.Equal(new TimeOnly(19, 30), slots[^1].StartTime);
            Assert.All(slots, slot => Assert.True(slot.IsAvailable));
        }

        [Fact]
        public async Task GetAvailableSlots_FortyFiveMinuteDuration_ReturnsFourteenGridSlots()
        {
            var slots = await _scheduleService.GetAvailableSlotsAsync(_doctorId, Saturday, 45);

            Assert.Equal(14, slots.Count);
            Assert.Equal(new TimeOnly(16, 0), slots[0].StartTime);
            Assert.Equal(new TimeOnly(19, 15), slots[^1].StartTime);
            Assert.Equal(new TimeOnly(20, 0), slots[^1].EndTime);
            Assert.All(slots, slot => Assert.True(slot.IsAvailable));
        }

        [Fact]
        public async Task GetAvailableSlots_FifteenMinuteDuration_ReturnsSixteenGridSlots()
        {
            var slots = await _scheduleService.GetAvailableSlotsAsync(_doctorId, Saturday, 15);

            Assert.Equal(16, slots.Count);
            Assert.Equal(new TimeOnly(16, 0), slots[0].StartTime);
            Assert.Equal(new TimeOnly(19, 45), slots[^1].StartTime);
            Assert.Equal(new TimeOnly(20, 0), slots[^1].EndTime);
            Assert.All(slots, slot => Assert.True(slot.IsAvailable));
        }

        [Fact]
        public async Task GetAvailableSlots_SixtyMinuteDuration_ReturnsThirteenAvailableSlots()
        {
            var slots = await _scheduleService.GetAvailableSlotsAsync(_doctorId, Saturday, 60);

            Assert.Equal(13, slots.Count);
            Assert.Equal(new TimeOnly(19, 0), slots[^1].StartTime);
            Assert.All(slots, slot => Assert.True(slot.IsAvailable));
        }

        [Fact]
        public async Task GetAvailableSlots_DayOff_ReturnsNoSlots()
        {
            await _doctorService.AddScheduleExceptionAsync(
                _doctorId,
                Saturday,
                ScheduleExceptionType.DayOff,
                null,
                null,
                null);

            var slots = await _scheduleService.GetAvailableSlotsAsync(_doctorId, Saturday, 30);

            Assert.Empty(slots);
        }

        [Fact]
        public async Task GetAvailableSlots_BookedAppointment_RemovesSlot()
        {
            var result = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(16, 30), 30));
            Assert.True(result.Success);

            var slots = await _scheduleService.GetAvailableSlotsAsync(_doctorId, Saturday, 30);

            var booked = Assert.Single(slots, s => s.StartTime == new TimeOnly(16, 30));
            Assert.False(booked.IsAvailable);
            Assert.Equal(12, slots.Count(s => s.IsAvailable));
        }

        [Fact]
        public async Task GetAvailableSlotsResponse_DoctorDoesNotWork_ReturnsMessage()
        {
            var result = await _scheduleService.GetAvailableSlotsResponseAsync(_doctorId, Friday, 30, "Dr. Test");

            Assert.Empty(result.Slots);
            Assert.Null(result.NextAvailableStart);
            Assert.Equal("Dr. Test does not work on this date.", result.Message);
        }

        [Fact]
        public async Task GetAvailableSlotsResponse_DayOff_ReturnsMessage()
        {
            await _doctorService.AddScheduleExceptionAsync(
                _doctorId,
                Saturday,
                ScheduleExceptionType.DayOff,
                null,
                null,
                null);

            var result = await _scheduleService.GetAvailableSlotsResponseAsync(_doctorId, Saturday, 30, "Dr. Test");

            Assert.Empty(result.Slots);
            Assert.Null(result.NextAvailableStart);
            Assert.Equal("Dr. Test is unavailable on this date.", result.Message);
        }

        [Fact]
        public async Task GetAvailableSlotsResponse_ReturnsNextAvailableStart()
        {
            var result = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(16, 0), 30));
            Assert.True(result.Success);

            var response = await _scheduleService.GetAvailableSlotsResponseAsync(_doctorId, Saturday, 30, "Dr. Test");

            Assert.Equal(new TimeOnly(16, 30), response.NextAvailableStart);
            Assert.Null(response.Message);
        }

        [Fact]
        public async Task GetAvailableSlotsResponse_AllBooked_ReturnsNoSlotsMessage()
        {
            foreach (var start in new[] { 16, 17, 18, 19 })
            {
                var result = await _appointmentService.CreateAppointmentAsync(
                    new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(start, 0), 60));
                Assert.True(result.Success);
            }

            var response = await _scheduleService.GetAvailableSlotsResponseAsync(_doctorId, Saturday, 60, "Dr. Test");

            Assert.Equal(13, response.Slots.Count);
            Assert.DoesNotContain(response.Slots, s => s.IsAvailable);
            Assert.Null(response.NextAvailableStart);
            Assert.Equal("No available slots remain for this date.", response.Message);
        }

        [Fact]
        public async Task GetAvailableSlotsResponse_NoFitInRemainingWorkingHours_ReturnsMessage()
        {
            var added = await _doctorService.AddScheduleExceptionAsync(
                _doctorId,
                Saturday,
                ScheduleExceptionType.ModifiedHours,
                new TimeOnly(19, 0),
                new TimeOnly(20, 0),
                "Short shift");
            Assert.True(added);

            var now = new DateTime(2026, 8, 15, 19, 30, 0);
            var response = await _scheduleService.GetAvailableSlotsResponseAsync(_doctorId, Saturday, 60, "Dr. Test", now);

            Assert.Empty(response.Slots);
            Assert.Null(response.NextAvailableStart);
            Assert.Equal("No appointment of the selected duration can fit within the remaining working hours.", response.Message);
        }

        [Fact]
        public async Task GetDailySchedule_ReturnsPeriodsAppointmentsAndAvailableSlots()
        {
            var created = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(17, 0), 30));
            Assert.True(created.Success);

            var schedule = await _scheduleService.GetDailyScheduleAsync(_doctorId, "Dr. Test", Saturday, 30);

            Assert.NotNull(schedule);
            Assert.True(schedule.IsWorking);
            Assert.False(schedule.IsDayOff);
            Assert.Single(schedule.WorkingPeriods);

            var appointment = Assert.Single(schedule.Appointments);
            Assert.Equal(new TimeOnly(17, 0), appointment.StartTime);
            Assert.Equal("Patient One", appointment.PatientName);

            Assert.Equal(new TimeOnly(16, 0), schedule.NextAvailableStart);
            Assert.Equal(15, schedule.Slots.Count);
            Assert.Null(schedule.Message);
        }

        [Fact]
        public async Task GetDailySchedule_UnknownDoctor_ReturnsNull()
        {
            var schedule = await _scheduleService.GetDailyScheduleAsync(9999, "Dr. Test", Saturday, 30);

            Assert.Null(schedule);
        }

        [Fact]
        public async Task CreateAppointment_AdjacentSlot_IsAllowed()
        {
            var first = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(16, 0), 30));
            Assert.True(first.Success);

            var second = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(16, 30), 30));
            Assert.True(second.Success);
        }

        [Fact]
        public async Task CreateAppointment_FifteenMinuteDuration_SetsCorrectEndTime()
        {
            var result = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(16, 0), 15));
            Assert.True(result.Success);

            var appointment = await _db.Appointments.SingleAsync(a => a.StartTime == new TimeOnly(16, 0));
            Assert.Equal(new TimeOnly(16, 15), appointment.EndTime);
        }

        [Fact]
        public async Task CreateAppointment_InvalidDuration_IsRejected()
        {
            var result = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(16, 0), 20));

            Assert.False(result.Success);
            Assert.Contains("duration", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateAppointment_OverlappingSlot_IsRejected()
        {
            var first = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(16, 0), 30));
            Assert.True(first.Success);

            var overlapping = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(16, 15), 30));
            Assert.False(overlapping.Success);
            Assert.Contains("no longer available", overlapping.ErrorMessage);
        }

        [Fact]
        public async Task CreateAppointment_SameStartTime_IsRejected()
        {
            var first = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(17, 0), 30));
            Assert.True(first.Success);

            var duplicate = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(17, 0), 30));
            Assert.False(duplicate.Success);
        }

        [Fact]
        public async Task CreateAppointment_OutsideWorkingHours_IsRejected()
        {
            var result = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(20, 0), 30));
            Assert.False(result.Success);
            Assert.Contains("working hours", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateAppointment_DoctorDoesNotWorkThatDay_IsRejected()
        {
            var result = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Friday, new TimeOnly(16, 0), 30));
            Assert.False(result.Success);
            Assert.Contains("not available", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateAppointment_UnknownPatient_IsRejected()
        {
            var result = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(9999, _doctorId, Saturday, new TimeOnly(16, 0), 30));
            Assert.False(result.Success);
            Assert.Contains("patient does not exist", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateAppointment_CancelledAppointment_FreesTheSlot()
        {
            var create = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(18, 0), 30));
            Assert.True(create.Success);

            var appointment = await _db.Appointments.SingleAsync(a => a.StartTime == new TimeOnly(18, 0));
            var cancelled = await _appointmentService.CancelAppointmentAsync(appointment.AppointmentId);
            Assert.True(cancelled);

            var rebook = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(18, 0), 30));
            Assert.True(rebook.Success);
        }

        [Fact]
        public async Task CreateAppointment_PastDate_IsRejected()
        {
            var result = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, new DateOnly(2026, 1, 1), new TimeOnly(16, 0), 30));

            Assert.False(result.Success);
            Assert.Contains("past", result.ErrorMessage);
        }

        [Fact]
        public async Task AddScheduleException_PastDate_IsRejected()
        {
            var added = await _doctorService.AddScheduleExceptionAsync(
                _doctorId,
                new DateOnly(2026, 1, 1),
                ScheduleExceptionType.DayOff,
                null,
                null,
                null);

            Assert.False(added);
        }

        [Fact]
        public async Task UpdateAppointment_ChangesTime()
        {
            var create = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(16, 0), 30));
            Assert.True(create.Success);

            var appointment = await _db.Appointments.SingleAsync(a => a.StartTime == new TimeOnly(16, 0));

            var updated = await _appointmentService.UpdateAppointmentAsync(
                new UpdateAppointmentCommand(appointment.AppointmentId, _patientId, _doctorId, Saturday, new TimeOnly(17, 0), 30));
            Assert.True(updated.Success);

            var reloaded = await _appointmentService.GetAppointmentAsync(appointment.AppointmentId);
            Assert.Equal(new TimeOnly(17, 0), reloaded!.StartTime);
            Assert.Equal(new TimeOnly(17, 30), reloaded.EndTime);
        }

        [Fact]
        public async Task UpdateAppointment_OverlappingSlot_IsRejected()
        {
            var first = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(16, 0), 30));
            Assert.True(first.Success);

            var second = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(17, 0), 30));
            Assert.True(second.Success);

            var appointment = await _db.Appointments.SingleAsync(a => a.StartTime == new TimeOnly(16, 0));

            var updated = await _appointmentService.UpdateAppointmentAsync(
                new UpdateAppointmentCommand(appointment.AppointmentId, _patientId, _doctorId, Saturday, new TimeOnly(17, 15), 30));
            Assert.False(updated.Success);
            Assert.Contains("no longer available", updated.ErrorMessage);
        }

        [Fact]
        public async Task UpdateAppointment_PastDate_IsRejected()
        {
            var create = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(16, 0), 30));
            Assert.True(create.Success);

            var appointment = await _db.Appointments.SingleAsync(a => a.StartTime == new TimeOnly(16, 0));

            var updated = await _appointmentService.UpdateAppointmentAsync(
                new UpdateAppointmentCommand(appointment.AppointmentId, _patientId, _doctorId, new DateOnly(2026, 1, 1), new TimeOnly(16, 0), 30));
            Assert.False(updated.Success);
            Assert.Contains("past", updated.ErrorMessage);
        }

        [Fact]
        public async Task UpdateAppointment_UnknownAppointment_IsRejected()
        {
            var updated = await _appointmentService.UpdateAppointmentAsync(
                new UpdateAppointmentCommand(9999, _patientId, _doctorId, Saturday, new TimeOnly(16, 0), 30));
            Assert.False(updated.Success);
        }

        [Fact]
        public async Task UpdateAppointment_CancelledAppointment_IsRejected()
        {
            var create = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(_patientId, _doctorId, Saturday, new TimeOnly(16, 0), 30));
            Assert.True(create.Success);

            var appointment = await _db.Appointments.SingleAsync(a => a.StartTime == new TimeOnly(16, 0));
            await _appointmentService.CancelAppointmentAsync(appointment.AppointmentId);

            var updated = await _appointmentService.UpdateAppointmentAsync(
                new UpdateAppointmentCommand(appointment.AppointmentId, _patientId, _doctorId, Saturday, new TimeOnly(17, 0), 30));
            Assert.False(updated.Success);
            Assert.Contains("cancelled", updated.ErrorMessage);
        }

        [Fact]
        public async Task CreatePatient_NormalizesWhitespace()
        {
            var id = await _patientService.CreatePatientAsync(new PatientDto
            {
                Name = "  Ali   Hassan ",
                BirthDate = new DateOnly(1990, 1, 1),
                Gender = Gender.Male,
                Phone = "  0123  "
            });

            var patient = await _patientService.GetPatientAsync(id);

            Assert.Equal("Ali Hassan", patient!.Name);
            Assert.Equal("0123", patient.Phone);
        }

        public void Dispose()
        {
            _db.Dispose();
            _connection.Dispose();
        }
    }
}
