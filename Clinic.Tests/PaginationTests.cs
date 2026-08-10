using Clinic.Data;
using Clinic.Models.Entities;
using Clinic.Models.Enums;
using Clinic.Services;
using ClinicEntity = Clinic.Models.Entities.Clinic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clinic.Tests
{
    public class PaginationTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ClinicDbContext _db;
        private readonly AppointmentService _appointmentService;
        private readonly PatientService _patientService;
        private readonly DoctorService _doctorService;

        private static readonly DateOnly Saturday = new(2026, 8, 15);

        public PaginationTests()
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

            _doctorService = new DoctorService(_db);
            _patientService = new PatientService(_db);
            _appointmentService = new AppointmentService(
                _db,
                new ScheduleService(_db),
                NullLogger<AppointmentService>.Instance);

            var doctorId = _doctorService.CreateDoctorAsync(new Models.Dtos.DoctorDto
            {
                ClinicId = clinic.ClinicId,
                Name = "Dr. Pagination",
                Specialization = "General Medicine",
                Phone = "0100"
            }).GetAwaiter().GetResult();

            for (var i = 2; i <= 12; i++)
            {
                _doctorService.CreateDoctorAsync(new Models.Dtos.DoctorDto
                {
                    ClinicId = clinic.ClinicId,
                    Name = $"Dr. {i:D2}",
                    Specialization = "General Medicine",
                    Phone = $"010{i:D3}"
                }).GetAwaiter().GetResult();
            }

            var patientId = _patientService.CreatePatientAsync(new Models.Dtos.PatientDto
            {
                Name = "Patient 01",
                BirthDate = new DateOnly(1990, 1, 1),
                Gender = Gender.Male,
                Phone = "0111"
            }).GetAwaiter().GetResult();

            for (var i = 2; i <= 12; i++)
            {
                _patientService.CreatePatientAsync(new Models.Dtos.PatientDto
                {
                    Name = $"Patient {i:D2}",
                    BirthDate = new DateOnly(1990, 1, 1),
                    Gender = Gender.Male,
                    Phone = $"011{i:D3}"
                }).GetAwaiter().GetResult();
            }

            for (var i = 0; i < 12; i++)
            {
                var start = new TimeOnly(i / 2, i % 2 == 0 ? 0 : 30);
                _db.Appointments.Add(new Appointment
                {
                    DoctorId = doctorId,
                    PatientId = patientId,
                    AppointmentDate = Saturday,
                    StartTime = start,
                    EndTime = start.AddMinutes(30),
                    Status = i == 11 ? AppointmentStatus.Completed : AppointmentStatus.Scheduled,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _db.SaveChanges();
        }

        [Fact]
        public async Task GetAppointmentsPaged_FirstPage_ReturnsPageAndMetadata()
        {
            var paged = await _appointmentService.GetAppointmentsPagedAsync(Saturday, null, null, null, 1, 10);

            Assert.Equal(12, paged.TotalCount);
            Assert.Equal(2, paged.TotalPages);
            Assert.Equal(1, paged.CurrentPage);
            Assert.Equal(10, paged.Items.Count);
        }

        [Fact]
        public async Task GetAppointmentsPaged_SecondPage_ReturnsRemainingItems()
        {
            var paged = await _appointmentService.GetAppointmentsPagedAsync(Saturday, null, null, null, 2, 10);

            Assert.Equal(2, paged.Items.Count);
            Assert.Equal(new TimeOnly(5, 0), paged.Items[0].StartTime);
            Assert.Equal(new TimeOnly(5, 30), paged.Items[1].StartTime);
        }

        [Fact]
        public async Task GetAppointmentsPaged_StatusFilter_IsAppliedBeforePagination()
        {
            var paged = await _appointmentService.GetAppointmentsPagedAsync(Saturday, null, null, AppointmentStatus.Scheduled, 1, 10);

            Assert.Equal(11, paged.TotalCount);
            Assert.Equal(10, paged.Items.Count);
            Assert.All(paged.Items, a => Assert.Equal("Scheduled", a.Status));
        }

        [Fact]
        public async Task GetPatientsPaged_SearchAndPagination()
        {
            var paged = await _patientService.GetPagedAsync("Patient", 1, 10);

            Assert.Equal(12, paged.TotalCount);
            Assert.Equal(2, paged.TotalPages);
            Assert.Equal(10, paged.Items.Count);
            Assert.All(paged.Items, p => Assert.Contains("Patient", p.Name));

            var page2 = await _patientService.GetPagedAsync("Patient", 2, 10);
            Assert.Equal(2, page2.Items.Count);

            var none = await _patientService.GetPagedAsync("Nobody", 1, 10);
            Assert.Equal(0, none.TotalCount);
            Assert.Empty(none.Items);
        }

        [Fact]
        public async Task GetDoctorsPaged_Paginates()
        {
            var paged = await _doctorService.GetDoctorsPagedAsync(1, 10);

            Assert.Equal(12, paged.TotalCount);
            Assert.Equal(2, paged.TotalPages);
            Assert.Equal(10, paged.Items.Count);

            var page2 = await _doctorService.GetDoctorsPagedAsync(2, 10);
            Assert.Equal(2, page2.Items.Count);
        }

        public void Dispose()
        {
            _db.Dispose();
            _connection.Dispose();
        }
    }
}
