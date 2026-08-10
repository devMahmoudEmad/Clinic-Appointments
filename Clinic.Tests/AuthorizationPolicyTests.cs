using Clinic.Controllers;
using Clinic.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace Clinic.Tests
{
    public class AuthorizationPolicyTests
    {
        private const string BothRoles = ApplicationRoles.Admin + "," + ApplicationRoles.Secretary;

        private static AuthorizeAttribute? ClassAuthorize<T>()
        {
            return typeof(T)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .FirstOrDefault();
        }

        private static AuthorizeAttribute? ActionAuthorize<T>(string actionName, params Type[] parameterTypes)
        {
            var method = typeof(T).GetMethod(actionName, parameterTypes);
            Assert.NotNull(method);

            return method!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .FirstOrDefault();
        }

        [Fact]
        public void AppointmentController_RequiresAdminOrSecretary()
        {
            var attribute = ClassAuthorize<AppointmentController>();

            Assert.NotNull(attribute);
            Assert.Equal(BothRoles, attribute!.Roles);
        }

        [Fact]
        public void PatientController_RequiresAdminOrSecretary()
        {
            var attribute = ClassAuthorize<PatientController>();

            Assert.NotNull(attribute);
            Assert.Equal(BothRoles, attribute!.Roles);
        }

        [Fact]
        public void DoctorController_RequiresAdmin()
        {
            var attribute = ClassAuthorize<DoctorController>();

            Assert.NotNull(attribute);
            Assert.Equal(ApplicationRoles.Admin, attribute!.Roles);
        }

        [Fact]
        public void DoctorController_ViewActions_AreAdminOnly()
        {
            // Index and Details carry no action-level attribute, so the class-level
            // Admin requirement applies to them too. Secretaries must not reach any
            // doctor page.
            var index = ActionAuthorize<DoctorController>(nameof(DoctorController.Index), typeof(int));
            var details = ActionAuthorize<DoctorController>(nameof(DoctorController.Details), typeof(int));

            Assert.Null(index);
            Assert.Null(details);
            Assert.Equal(ApplicationRoles.Admin, ClassAuthorize<DoctorController>()!.Roles);
        }

        [Fact]
        public void DoctorController_Create_RequiresAdmin()
        {
            var get = ActionAuthorize<DoctorController>(nameof(DoctorController.Create));
            var post = ActionAuthorize<DoctorController>(
                nameof(DoctorController.Create), typeof(Clinic.ViewModels.Doctors.DoctorCreateViewModel));

            Assert.Equal(ApplicationRoles.Admin, get!.Roles);
            Assert.Equal(ApplicationRoles.Admin, post!.Roles);
        }

        [Fact]
        public void DoctorController_Edit_RequiresAdmin()
        {
            var get = ActionAuthorize<DoctorController>(nameof(DoctorController.Edit), typeof(int));
            var post = ActionAuthorize<DoctorController>(
                nameof(DoctorController.Edit), typeof(Clinic.ViewModels.Doctors.DoctorEditViewModel));

            Assert.Equal(ApplicationRoles.Admin, get!.Roles);
            Assert.Equal(ApplicationRoles.Admin, post!.Roles);
        }

        [Fact]
        public void DoctorController_ScheduleAndExceptionActions_RequireAdmin()
        {
            Assert.Equal(
                ApplicationRoles.Admin,
                ActionAuthorize<DoctorController>(
                    nameof(DoctorController.AddWeeklySchedule),
                    typeof(int),
                    typeof(Clinic.ViewModels.Doctors.DoctorDetailsViewModel))!.Roles);

            Assert.Equal(
                ApplicationRoles.Admin,
                ActionAuthorize<DoctorController>(
                    nameof(DoctorController.RemoveWeeklySchedule),
                    typeof(int),
                    typeof(int))!.Roles);

            Assert.Equal(
                ApplicationRoles.Admin,
                ActionAuthorize<DoctorController>(
                    nameof(DoctorController.AddException),
                    typeof(int),
                    typeof(Clinic.ViewModels.Doctors.DoctorDetailsViewModel))!.Roles);

            Assert.Equal(
                ApplicationRoles.Admin,
                ActionAuthorize<DoctorController>(
                    nameof(DoctorController.RemoveException),
                    typeof(int),
                    typeof(int))!.Roles);
        }

        [Fact]
        public void UserController_RequiresAdmin()
        {
            var attribute = ClassAuthorize<UserController>();

            Assert.NotNull(attribute);
            Assert.Equal(ApplicationRoles.Admin, attribute!.Roles);
        }

        [Fact]
        public void AccountController_Login_IsPublic()
        {
            var attribute = typeof(AccountController)
                .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
                .Cast<AllowAnonymousAttribute>()
                .FirstOrDefault();

            Assert.NotNull(attribute);
        }

        [Fact]
        public void ApplicationRoles_AreExactlyAdminAndSecretary()
        {
            Assert.Equal(new[] { "Admin", "Secretary" }, ApplicationRoles.All);
        }
    }
}
