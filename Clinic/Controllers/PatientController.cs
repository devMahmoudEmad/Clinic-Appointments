using Clinic.Models.Dtos;
using Clinic.Models.Enums;
using Clinic.Security;
using Clinic.Services;
using Clinic.ViewModels.Patients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Controllers
{
    [Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Secretary)]
    public class PatientController : Controller
    {
        private readonly IPatientService _patientService;

        public PatientController(IPatientService patientService)
        {
            _patientService = patientService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? searchTerm, int page = 1)
        {
            page = Math.Max(1, page);

            var paged = await _patientService.GetPagedAsync(searchTerm, page, PageSize);

            return View(new PatientSearchViewModel
            {
                SearchTerm = searchTerm,
                Patients = paged.Items,
                CurrentPage = paged.CurrentPage,
                TotalPages = paged.TotalPages
            });
        }

        [HttpGet]
        public async Task<IActionResult> SearchAjax(string? term)
        {
            var patients = await _patientService.SearchPatientsAsync(term, limit: 20);

            return Json(patients.Select(p => new
            {
                p.PatientId,
                p.Name,
                p.Phone,
                p.BirthDate,
                p.Gender
            }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAjax([FromBody] PatientCreateViewModel viewModel)
        {
            Clinic.Validation.ModelStateErrorMapper.ReplaceTechnicalErrors(ModelState);

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(ms => ms.Value!.Errors.Count > 0)
                    .SelectMany(ms => ms.Value!.Errors.Select(e => new
                    {
                        Key = Clinic.Validation.ModelStateErrorMapper.NormalizeKey(ms.Key),
                        Message = string.IsNullOrWhiteSpace(e.ErrorMessage)
                            ? "Please check this field."
                            : e.ErrorMessage
                    }))
                    .GroupBy(e => e.Key)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Message).Distinct().ToArray());

                return Json(new { success = false, errors });
            }

            var patientId = await _patientService.CreatePatientAsync(new PatientDto
            {
                Name = viewModel.Name,
                BirthDate = viewModel.BirthDate!.Value,
                Gender = viewModel.Gender!.Value,
                Phone = viewModel.Phone
            });

            return Json(new
            {
                success = true,
                patientId,
                name = viewModel.Name,
                phone = viewModel.Phone
            });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new PatientCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PatientCreateViewModel viewModel)
        {
            Clinic.Validation.ModelStateErrorMapper.ReplaceTechnicalErrors(ModelState);

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            await _patientService.CreatePatientAsync(new PatientDto
            {
                Name = viewModel.Name,
                BirthDate = viewModel.BirthDate!.Value,
                Gender = viewModel.Gender!.Value,
                Phone = viewModel.Phone
            });

            TempData["Success"] = "Patient registered successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var patient = await _patientService.GetPatientAsync(id);
            if (patient is null)
            {
                return NotFound();
            }

            return View(new PatientEditViewModel
            {
                PatientId = patient.PatientId,
                Name = patient.Name,
                BirthDate = patient.BirthDate,
                Gender = patient.Gender,
                Phone = patient.Phone
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PatientEditViewModel viewModel)
        {
            Clinic.Validation.ModelStateErrorMapper.ReplaceTechnicalErrors(ModelState);

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            var updated = await _patientService.UpdatePatientAsync(new PatientDto
            {
                PatientId = viewModel.PatientId,
                Name = viewModel.Name,
                BirthDate = viewModel.BirthDate!.Value,
                Gender = viewModel.Gender!.Value,
                Phone = viewModel.Phone
            });

            if (!updated)
            {
                return NotFound();
            }

            TempData["Success"] = "Patient updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        private const int PageSize = 10;
    }
}
