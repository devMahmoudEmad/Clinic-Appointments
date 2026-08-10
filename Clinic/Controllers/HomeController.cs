using Clinic.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Clinic.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction(nameof(AppointmentController.Index), "Appointment");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? statusCode)
        {
            var message = statusCode switch
            {
                400 => "The request was invalid.",
                401 => "You must sign in to access this page.",
                403 => "You don't have permission to access this page.",
                404 => "The page you requested was not found.",
                500 => "An unexpected error occurred.",
                _ => "An error occurred while processing your request."
            };

            if (statusCode.HasValue)
            {
                Response.StatusCode = statusCode.Value;
            }

            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = statusCode,
                Message = message
            });
        }
    }
}
