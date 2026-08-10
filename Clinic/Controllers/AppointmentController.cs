using Clinic.Models.Dtos;
using Clinic.Models.Enums;
using Clinic.Security;
using Clinic.Services;
using Clinic.ViewModels.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Clinic.Controllers
{
    [Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Secretary)]
    public class AppointmentController : Controller
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IDoctorService _doctorService;
        private readonly IPatientService _patientService;

        public AppointmentController(
            IAppointmentService appointmentService,
            IDoctorService doctorService,
            IPatientService patientService)
        {
            _appointmentService = appointmentService;
            _doctorService = doctorService;
            _patientService = patientService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            DateOnly? date,
            int? doctorId,
            string? patientName,
            AppointmentStatus? status,
            int page = 1)
        {
            Clinic.Validation.ModelStateErrorMapper.ReplaceTechnicalErrors(ModelState);
            date ??= DateOnly.FromDateTime(DateTime.Today);
            page = Math.Max(1, page);

            var paged = await _appointmentService
                .GetAppointmentsPagedAsync(date, doctorId, patientName, status, page, PageSize);

            var viewModel = new AppointmentIndexViewModel
            {
                Date = date,
                DoctorId = doctorId,
                PatientName = patientName,
                Status = status,
                Appointments = paged.Items
                    .Select(a => new AppointmentRowViewModel
                    {
                        AppointmentId = a.AppointmentId,
                        AppointmentDate = a.AppointmentDate,
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        Status = a.Status,
                        PatientName = a.PatientName,
                        DoctorName = a.DoctorName
                    })
                    .ToList(),
                Doctors = await GetDoctorSelectListAsync(doctorId),
                CurrentPage = paged.CurrentPage,
                TotalPages = paged.TotalPages
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? patientId)
        {
            var viewModel = new AppointmentCreateViewModel
            {
                AppointmentDate = DateOnly.FromDateTime(DateTime.Today),
                Doctors = await GetDoctorSelectListAsync()
            };

            if (patientId.HasValue)
            {
                var patient = await _patientService.GetPatientAsync(patientId.Value);
                if (patient is not null)
                {
                    viewModel.PatientId = patient.PatientId;
                    viewModel.PatientName = patient.Name;
                }
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AppointmentCreateViewModel viewModel)
        {
            Clinic.Validation.ModelStateErrorMapper.ReplaceTechnicalErrors(ModelState);

            if (!ModelState.IsValid)
            {
                viewModel.Doctors = await GetDoctorSelectListAsync(viewModel.DoctorId);
                return View(viewModel);
            }

            var result = await _appointmentService.CreateAppointmentAsync(
                new CreateAppointmentCommand(
                    viewModel.PatientId,
                    viewModel.DoctorId,
                    viewModel.AppointmentDate!.Value,
                    viewModel.StartTime!.Value,
                    viewModel.DurationMinutes));

            if (result.Success)
            {
                TempData["Success"] = "Appointment created successfully.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            viewModel.Doctors = await GetDoctorSelectListAsync(viewModel.DoctorId);
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var appointment = await _appointmentService.GetAppointmentAsync(id);
            if (appointment is null)
            {
                return NotFound();
            }

            if (appointment.Status == nameof(AppointmentStatus.Cancelled))
            {
                TempData["Message"] = "A cancelled appointment cannot be edited.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = new AppointmentEditViewModel
            {
                AppointmentId = appointment.AppointmentId,
                PatientId = appointment.PatientId,
                PatientName = appointment.PatientName,
                DoctorId = appointment.DoctorId,
                AppointmentDate = appointment.AppointmentDate,
                StartTime = appointment.StartTime,
                DurationMinutes = GetDurationMinutes(appointment.StartTime, appointment.EndTime),
                Doctors = await GetDoctorSelectListAsync(appointment.DoctorId)
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AppointmentEditViewModel viewModel)
        {
            Clinic.Validation.ModelStateErrorMapper.ReplaceTechnicalErrors(ModelState);

            if (!ModelState.IsValid)
            {
                viewModel.Doctors = await GetDoctorSelectListAsync(viewModel.DoctorId);
                return View(viewModel);
            }

            var result = await _appointmentService.UpdateAppointmentAsync(
                new UpdateAppointmentCommand(
                    viewModel.AppointmentId,
                    viewModel.PatientId,
                    viewModel.DoctorId,
                    viewModel.AppointmentDate!.Value,
                    viewModel.StartTime!.Value,
                    viewModel.DurationMinutes));

            if (result.Success)
            {
                TempData["Success"] = "Appointment updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            viewModel.Doctors = await GetDoctorSelectListAsync(viewModel.DoctorId);
            return View(viewModel);
        }

        private static int GetDurationMinutes(TimeOnly startTime, TimeOnly endTime)
        {
            return (int)(endTime - startTime).TotalMinutes;
        }

        [HttpGet]
        public async Task<IActionResult> AvailableSlots(int doctorId, DateOnly date, int durationMinutes = 30)
        {
            var doctor = await _doctorService.GetDoctorAsync(doctorId);
            if (doctor is null)
            {
                return Json(new
                {
                    doctorNotFound = true,
                    slots = Array.Empty<object>(),
                    nextAvailable = (object?)null,
                    message = (object?)null
                });
            }

            // Only the durations the appointment form offers may be previewed,
            // so the slots shown always match what can actually be booked.
            if (durationMinutes is not (15 or 30 or 45 or 60))
            {
                durationMinutes = 30;
            }

            var result = await _appointmentService.GetAvailableSlotsResponseAsync(
                doctorId,
                date,
                durationMinutes,
                doctor.Name);

            return Json(new
            {
                doctorNotFound = false,
                slots = result.Slots,
                nextAvailable = result.NextAvailableStart?.ToString("HH:mm"),
                message = result.Message
            });
        }

        [HttpGet]
        public async Task<IActionResult> DailySchedule(int? doctorId, DateOnly? date, int durationMinutes = 30)
        {
            if (durationMinutes is not (15 or 30 or 45 or 60))
            {
                durationMinutes = AllowedAppointmentDurationsAttribute.ThirtyMinutes;
            }

            // On a fresh GET there is nothing to validate; clear the required
            // errors that model binding adds so the empty form shows no error.
            if (!Request.Query.ContainsKey("date"))
            {
                ModelState.Clear();
            }

            var viewModel = new DailyScheduleViewModel
            {
                DoctorId = doctorId,
                Date = date,
                DurationMinutes = durationMinutes,
                Doctors = await GetDoctorSelectListAsync(doctorId)
            };

            if (!doctorId.HasValue || !date.HasValue)
            {
                return View(viewModel);
            }

            var doctor = await _doctorService.GetDoctorAsync(doctorId.Value);
            if (doctor is null)
            {
                viewModel.Message = "The selected doctor no longer exists.";
                return View(viewModel);
            }

            var schedule = await _appointmentService.GetDailyScheduleAsync(
                doctorId.Value,
                doctor.Name,
                date.Value,
                durationMinutes);

            if (schedule is null)
            {
                viewModel.Message = "The selected doctor no longer exists.";
                return View(viewModel);
            }

            viewModel.DoctorName = doctor.Name;
            viewModel.IsWorking = schedule.IsWorking;
            viewModel.IsDayOff = schedule.IsDayOff;
            viewModel.WorkingPeriods = schedule.WorkingPeriods;
            viewModel.NextAvailableSlot = schedule.NextAvailableStart;
            viewModel.Message = schedule.Message;

            var rows = schedule.Appointments
                .Select(a => new DailyScheduleRow
                {
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Status = "Booked",
                    PatientName = a.PatientName
                })
                .Concat(schedule.Slots
                    .Where(s => s.IsAvailable)
                    .Select(s => new DailyScheduleRow
                    {
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Status = "Available",
                        PatientName = string.Empty
                    }))
                .OrderBy(r => r.StartTime)
                .ToList();

            viewModel.Rows = rows;

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> DailyScheduleData(int doctorId, DateOnly date, int durationMinutes = 30)
        {
            var doctor = await _doctorService.GetDoctorAsync(doctorId);
            if (doctor is null)
            {
                return Json(new { doctorNotFound = true });
            }

            if (durationMinutes is not (15 or 30 or 45 or 60))
            {
                durationMinutes = 30;
            }

            var schedule = await _appointmentService.GetDailyScheduleAsync(
                doctorId,
                doctor.Name,
                date,
                durationMinutes);

            if (schedule is null)
            {
                return Json(new { doctorNotFound = true });
            }

            string? message = null;
            if (!schedule.IsWorking)
            {
                message = "Doctor is not working on this date.";
            }
            else if (schedule.Appointments.Count == 0)
            {
                message = "No appointments scheduled.";
            }

            return Json(new
            {
                doctorNotFound = false,
                doctorName = doctor.Name,
                date = date.ToString("dd/MM/yyyy"),
                isWorking = schedule.IsWorking,
                workingPeriods = schedule.WorkingPeriods.Select(p => new
                {
                    start = p.StartTime.ToString("HH:mm"),
                    end = p.EndTime.ToString("HH:mm")
                }),
                appointments = schedule.Appointments.Select(a => new
                {
                    start = a.StartTime.ToString("HH:mm"),
                    end = a.EndTime.ToString("HH:mm"),
                    patient = a.PatientName,
                    status = a.Status.ToString()
                }),
                availableStarts = schedule.Slots
                    .Where(s => s.IsAvailable)
                    .Select(s => s.StartTime.ToString("HH:mm")),
                message
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            await _appointmentService.CancelAppointmentAsync(id);
            TempData["Success"] = "Appointment cancelled.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<SelectListItem>> GetDoctorSelectListAsync(int? selectedDoctorId = null)
        {
            var doctors = await _doctorService.GetDoctorsAsync();
            return doctors
                .Select(d => new SelectListItem
                {
                    Value = d.DoctorId.ToString(),
                    Text = $"{d.Name} — {d.Specialization}",
                    Selected = d.DoctorId == selectedDoctorId
                })
                .ToList();
        }

        private const int PageSize = 10;
    }
}
