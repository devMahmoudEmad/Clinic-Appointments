namespace Clinic.Security
{
    /// <summary>
    /// The two application roles. Authorization attributes and the seeder
    /// reference these names so they stay consistent.
    /// </summary>
    public static class ApplicationRoles
    {
        public const string Admin = "Admin";

        public const string Secretary = "Secretary";

        public static readonly string[] All = { Admin, Secretary };
    }
}
