using Clinic.Security;
using Clinic.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Controllers
{
    [Authorize(Roles = ApplicationRoles.Admin)]
    public class UserController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;

        public UserController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            page = Math.Max(1, page);

            var query = _userManager.Users.OrderBy(u => u.Email);

            var totalCount = await query.CountAsync();

            var users = await query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var viewModels = new List<UserListViewModel>();
            foreach (var user in users)
            {
                viewModels.Add(new UserListViewModel
                {
                    Email = user.Email ?? string.Empty,
                    Roles = (await _userManager.GetRolesAsync(user)).ToList()
                });
            }

            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)PageSize);

            return View(new UserIndexViewModel
            {
                Users = viewModels,
                CurrentPage = page,
                TotalPages = totalPages
            });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateUserViewModel
            {
                Roles = GetRoleSelectList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel viewModel)
        {
            viewModel.Roles = GetRoleSelectList(viewModel.Role);
            Clinic.Validation.ModelStateErrorMapper.ReplaceTechnicalErrors(ModelState);

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            var user = new IdentityUser
            {
                UserName = viewModel.Email,
                Email = viewModel.Email
            };

            var createResult = await _userManager.CreateAsync(user, viewModel.Password);
            if (!createResult.Succeeded)
            {
                AddIdentityErrors(createResult);
                return View(viewModel);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, viewModel.Role);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                AddIdentityErrors(roleResult);
                return View(viewModel);
            }

            TempData["Success"] = $"User '{viewModel.Email}' created with the {viewModel.Role} role.";
            return RedirectToAction(nameof(Index));
        }

        private List<SelectListItem> GetRoleSelectList(string? selectedRole = null)
        {
            return ApplicationRoles.All
                .Select(r => new SelectListItem
                {
                    Value = r,
                    Text = r,
                    Selected = r == selectedRole
                })
                .ToList();
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private const int PageSize = 10;
    }
}
