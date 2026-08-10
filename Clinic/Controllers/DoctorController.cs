using Clinic.Models.Dtos;
using Clinic.Models.Entities;
using Clinic.Models.Enums;
using Clinic.Security;
using Clinic.Services;
using Clinic.ViewModels.Doctors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Clinic.Controllers
{
    [Authorize(Roles = ApplicationRoles.Admin)]
    public class DoctorController : Controller
    {
        private readonly IDoctorService _doctorService;
        private readonly IClinicService _clinicService;

        public DoctorController(IDoctorService doctorService, IClinicService clinicService)
        {
            _doctorService = doctorService;
            _clinicService = clinicService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            page = Math.Max(1, page);

            var paged = await _doctorService.GetDoctorsPagedAsync(page, PageSize);

            return View(new DoctorIndexViewModel
            {
                Doctors = paged.Items,
                CurrentPage = paged.CurrentPage,
                TotalPages = paged.TotalPages
            });
        }

        [HttpGet]
        [Authorize(Roles = ApplicationRoles.Admin)]
        public async Task<IActionResult> Create()
        {
            return View(new DoctorCreateViewModel
            {
                Clinics = await GetClinicSelectListAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ApplicationRoles.Admin)]
        public async Task<IActionResult> Create(DoctorCreateViewModel viewModel)
        {
            Clinic.Validation.ModelStateErrorMapper.ReplaceTechnicalErrors(ModelState);

            if (!ModelState.IsValid)
            {
                viewModel.Clinics = await GetClinicSelectListAsync(viewModel.ClinicId);
                return View(viewModel);
            }

            await _doctorService.CreateDoctorAsync(new DoctorDto
            {
                ClinicId = viewModel.ClinicId,
                Name = viewModel.Name,
                Specialization = viewModel.Specialization,
                Phone = viewModel.Phone
            });

            TempData["Success"] = "Doctor added successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = ApplicationRoles.Admin)]
        public async Task<IActionResult> Edit(int id)
        {
            var doctor = await _doctorService.GetDoctorAsync(id);
            if (doctor is null)
            {
                return NotFound();
            }

            return View(new DoctorEditViewModel
            {
                DoctorId = doctor.DoctorId,
                ClinicId = doctor.ClinicId,
                Name = doctor.Name,
                Specialization = doctor.Specialization,
                Phone = doctor.Phone,
                Clinics = await GetClinicSelectListAsync(doctor.ClinicId)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ApplicationRoles.Admin)]
        public async Task<IActionResult> Edit(DoctorEditViewModel viewModel)
        {
            Clinic.Validation.ModelStateErrorMapper.ReplaceTechnicalErrors(ModelState);

            if (!ModelState.IsValid)
            {
                viewModel.Clinics = await GetClinicSelectListAsync(viewModel.ClinicId);
                return View(viewModel);
            }

            var updated = await _doctorService.UpdateDoctorAsync(new DoctorDto
            {
                DoctorId = viewModel.DoctorId,
                ClinicId = viewModel.ClinicId,
                Name = viewModel.Name,
                Specialization = viewModel.Specialization,
                Phone = viewModel.Phone
            });

            if (!updated)
            {
                return NotFound();
            }

            TempData["Success"] = "Doctor updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            return await DetailsView(id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ApplicationRoles.Admin)]
        public async Task<IActionResult> AddWeeklySchedule(int id, DoctorDetailsViewModel form)
        {
            Clinic.Validation.ModelStateErrorMapper.ReplaceTechnicalErrors(ModelState);

            if (!form.StartTime.HasValue)
            {
                ModelState.AddModelError(nameof(form.StartTime), "Start time is required.");
            }

            if (!form.EndTime.HasValue)
            {
                ModelState.AddModelError(nameof(form.EndTime), "End time is required.");
            }

            if (!ModelState.IsValid)
            {
                return await DetailsView(id);
            }

            var added = await _doctorService.AddWeeklyScheduleAsync(
                id,
                form.DayOfWeek,
                form.StartTime!.Value,
                form.EndTime!.Value);

            TempData["Message"] = added
                ? "Working period added."
                : "Could not add the working period. It may overlap an existing period or the times may be invalid.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ApplicationRoles.Admin)]
        public async Task<IActionResult> RemoveWeeklySchedule(int doctorId, int scheduleId)
        {
            await _doctorService.RemoveWeeklyScheduleAsync(doctorId, scheduleId);
            TempData["Message"] = "Working period removed.";
            return RedirectToAction(nameof(Details), new { id = doctorId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ApplicationRoles.Admin)]
        public async Task<IActionResult> AddException(int id, DoctorDetailsViewModel form)
        {
            Clinic.Validation.ModelStateErrorMapper.ReplaceTechnicalErrors(ModelState);

            if (!ModelState.IsValid)
            {
                return await DetailsView(id);
            }

            var added = await _doctorService.AddScheduleExceptionAsync(
                id,
                form.ExceptionDate,
                form.ExceptionType,
                form.StartTimeException,
                form.EndTimeException,
                form.Reason);

            TempData["Message"] = added
                ? "Schedule exception added."
                : "Could not add the exception. Check the date and times (one exception is allowed per doctor per date).";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ApplicationRoles.Admin)]
        public async Task<IActionResult> RemoveException(int doctorId, int exceptionId)
        {
            await _doctorService.RemoveScheduleExceptionAsync(doctorId, exceptionId);
            TempData["Message"] = "Schedule exception removed.";
            return RedirectToAction(nameof(Details), new { id = doctorId });
        }

        private async Task<IActionResult> DetailsView(int id)
        {
            var doctor = await _doctorService.GetDoctorAsync(id);
            if (doctor is null)
            {
                return NotFound();
            }

            return View(new DoctorDetailsViewModel
            {
                Doctor = doctor,
                WeeklySchedules = (await _doctorService.GetWeeklyScheduleAsync(id)).ToList(),
                ScheduleExceptions = (await _doctorService.GetScheduleExceptionsAsync(id)).ToList(),
                ExceptionDate = DateOnly.FromDateTime(DateTime.Today)
            });
        }

        private async Task<List<SelectListItem>> GetClinicSelectListAsync(int? selectedClinicId = null)
        {
            var clinics = await _clinicService.GetClinicsAsync();
            return clinics
                .Select(c => new SelectListItem
                {
                    Value = c.ClinicId.ToString(),
                    Text = c.Name,
                    Selected = c.ClinicId == selectedClinicId
                })
                .ToList();
        }

        private const int PageSize = 10;
    }
}
