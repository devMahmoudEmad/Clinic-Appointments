using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Clinic.Data
{
    /// <summary>
    /// Design-time factory used by EF Core tooling (e.g. dotnet ef migrations add)
    /// so it can build the DbContext without starting the web application.
    /// </summary>
    public class ClinicDbContextFactory : IDesignTimeDbContextFactory<ClinicDbContext>
    {
        public ClinicDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var options = new DbContextOptionsBuilder<ClinicDbContext>()
                .UseSqlServer(configuration.GetConnectionString("ClinicDb"))
                .Options;

            return new ClinicDbContext(options);
        }
    }
}
