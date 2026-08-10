using Clinic.Models.Dtos;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Clinic.ViewModels.Account
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required.")]
        public string Role { get; set; } = string.Empty;

        public List<SelectListItem> Roles { get; set; } = new();
    }

    public class UserIndexViewModel : IPagedViewModel
    {
        public IReadOnlyList<UserListViewModel> Users { get; set; } = new List<UserListViewModel>();

        public int CurrentPage { get; set; } = 1;

        public int TotalPages { get; set; }
    }

    public class UserListViewModel
    {
        public string Email { get; set; } = string.Empty;

        public IReadOnlyList<string> Roles { get; set; } = new List<string>();
    }
}
