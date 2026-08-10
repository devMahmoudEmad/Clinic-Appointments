using Clinic.Models.Entities;
using ClinicEntity = Clinic.Models.Entities.Clinic;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Data
{
    public class ClinicDbContext : IdentityDbContext<IdentityUser>
    {
        public ClinicDbContext(DbContextOptions<ClinicDbContext> options)
            : base(options)
        {
        }

        public DbSet<ClinicEntity> Clinics => Set<ClinicEntity>();

        public DbSet<Doctor> Doctors => Set<Doctor>();

        public DbSet<DoctorWeeklySchedule> DoctorWeeklySchedules => Set<DoctorWeeklySchedule>();

        public DbSet<ScheduleException> ScheduleExceptions => Set<ScheduleException>();

        public DbSet<Patient> Patients => Set<Patient>();

        public DbSet<Appointment> Appointments => Set<Appointment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureClinic(modelBuilder);
            ConfigureDoctor(modelBuilder);
            ConfigureWeeklySchedule(modelBuilder);
            ConfigureScheduleException(modelBuilder);
            ConfigurePatient(modelBuilder);
            ConfigureAppointment(modelBuilder);
        }

        private static void ConfigureClinic(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClinicEntity>(entity =>
            {
                entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            });
        }

        private static void ConfigureDoctor(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Doctor>(entity =>
            {
                entity.Property(d => d.Name).IsRequired().HasMaxLength(100);
                entity.Property(d => d.Specialization).IsRequired().HasMaxLength(100);
                entity.Property(d => d.Phone).IsRequired().HasMaxLength(50);

                entity.HasOne(d => d.Clinic)
                    .WithMany(c => c.Doctors)
                    .HasForeignKey(d => d.ClinicId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureWeeklySchedule(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DoctorWeeklySchedule>(entity =>
            {
                entity.HasKey(s => s.ScheduleId);

                entity.HasOne(s => s.Doctor)
                    .WithMany(d => d.WeeklySchedules)
                    .HasForeignKey(s => s.DoctorId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(s => s.DoctorId);

                entity.HasIndex(s => new { s.DoctorId, s.DayOfWeek, s.StartTime, s.EndTime })
                    .IsUnique();
            });
        }

        private static void ConfigureScheduleException(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScheduleException>(entity =>
            {
                entity.HasKey(e => e.ExceptionId);

                entity.Property(e => e.Reason).HasMaxLength(200);

                entity.HasOne(e => e.Doctor)
                    .WithMany(d => d.ScheduleExceptions)
                    .HasForeignKey(e => e.DoctorId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.DoctorId, e.ExceptionDate })
                    .IsUnique();
            });
        }

        private static void ConfigurePatient(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Patient>(entity =>
            {
                entity.Property(p => p.Name).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Phone).IsRequired().HasMaxLength(50);
            });
        }

        private static void ConfigureAppointment(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.HasOne(a => a.Doctor)
                    .WithMany(d => d.Appointments)
                    .HasForeignKey(a => a.DoctorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Patient)
                    .WithMany(p => p.Appointments)
                    .HasForeignKey(a => a.PatientId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(a => a.AppointmentDate);
                entity.HasIndex(a => a.DoctorId);
                entity.HasIndex(a => a.PatientId);

                // Backstop against double-booking on the fixed 30-minute grid.
                // The primary protection is the server-side overlap check inside a
                // Serializable transaction; this unique index is a secondary guard
                // only. It is NOT sufficient alone once variable-length appointments
                // exist (see AGENTS.md).
                entity.HasIndex(a => new { a.DoctorId, a.AppointmentDate, a.StartTime, a.Status })
                    .IsUnique();
            });
        }
    }
}
