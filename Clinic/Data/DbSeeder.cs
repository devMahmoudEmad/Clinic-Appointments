using Clinic.Models.Entities;
using Clinic.Security;
using ClinicEntity = Clinic.Models.Entities.Clinic;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Data
{
    /// <summary>
    /// Idempotent development seed data: a default clinic and a sample doctor
    /// with a Saturday–Thursday 16:00–20:00 weekly schedule (Friday off).
    ///
    /// Also seeds the two application roles (Admin, Secretary) and one
    /// development user per role. These credentials are DEVELOPMENT-ONLY and are
    /// read from the "Seed" configuration section (see appsettings.json); they
    /// must never be used as real production credentials.
    /// </summary>
    public static class DbSeeder
    {
        public static async Task SeedAsync(
            ClinicDbContext db,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration)
        {
            if (!await db.Clinics.AnyAsync())
            {
                db.Clinics.Add(new ClinicEntity { Name = "Main Clinic" });
                await db.SaveChangesAsync();
            }

            if (!await db.Doctors.AnyAsync())
            {
                var clinic = await db.Clinics.FirstAsync();

                var doctor = new Doctor
                {
                    ClinicId = clinic.ClinicId,
                    Name = "Dr. Ahmed",
                    Specialization = "General Medicine",
                    Phone = "01012345678"
                };

                db.Doctors.Add(doctor);
                await db.SaveChangesAsync();

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
                    db.DoctorWeeklySchedules.Add(new DoctorWeeklySchedule
                    {
                        DoctorId = doctor.DoctorId,
                        DayOfWeek = day,
                        StartTime = new TimeOnly(16, 0),
                        EndTime = new TimeOnly(20, 0),
                        IsActive = true
                    });
                }

                await db.SaveChangesAsync();
            }

            await SeedRolesAsync(roleManager);
            await SeedUsersAsync(userManager, configuration);
        }

        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            foreach (var role in ApplicationRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        private static async Task SeedUsersAsync(
            UserManager<IdentityUser> userManager,
            IConfiguration configuration)
        {
            var adminEmail = configuration.GetValue("Seed:AdminEmail", "admin@clinic.local");
            var secretaryEmail = configuration.GetValue("Seed:SecretaryEmail", "secretary@clinic.local");
            var adminPassword = configuration.GetValue("Seed:AdminPassword", "Admin@123456");
            var secretaryPassword = configuration.GetValue("Seed:SecretaryPassword", "Secretary@123456");

            await EnsureUserAsync(userManager, adminEmail, adminPassword, ApplicationRoles.Admin);
            await EnsureUserAsync(userManager, secretaryEmail, secretaryPassword, ApplicationRoles.Secretary);
        }

        private static async Task EnsureUserAsync(
            UserManager<IdentityUser> userManager,
            string email,
            string password,
            string role)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new IdentityUser { UserName = email, Email = email };

                var result = await userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    return;
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
