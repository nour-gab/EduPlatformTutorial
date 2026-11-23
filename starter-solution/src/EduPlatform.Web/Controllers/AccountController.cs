using System.ComponentModel.DataAnnotations;
using EduPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser>   _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly RoleManager<ApplicationRole>  _roles;

    public AccountController(
        UserManager<ApplicationUser>   users,
        SignInManager<ApplicationUser> signIn,
        RoleManager<ApplicationRole>  roles)
    {
        _users  = users;
        _signIn = signIn;
        _roles  = roles;
    }

    // ── Login ──────────────────────────────────────────────────────────────

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl ?? "/";
        return View();
    }

    [HttpPost, AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _users.FindByEmailAsync(vm.Email);
        if (user is null)
        {
            ModelState.AddModelError("", "Invalid credentials.");
            return View(vm);
        }

        var result = await _signIn.PasswordSignInAsync(
            user.UserName!, vm.Password, vm.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastActiveAtUtc = DateTime.UtcNow;
            await _users.UpdateAsync(user);
            return LocalRedirect(returnUrl ?? "/");
        }
        if (result.IsLockedOut)
        {
            ModelState.AddModelError("", "Account locked. Try again in 15 minutes.");
            return View(vm);
        }

        ModelState.AddModelError("", "Invalid credentials.");
        return View(vm);
    }

    // ── Register ───────────────────────────────────────────────────────────

    [HttpGet, AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl ?? "/";
        return View();
    }

    [HttpPost, AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = new ApplicationUser
        {
            UserName         = vm.Email,
            Email            = vm.Email,
            FullName         = vm.FullName,
            EmailConfirmed   = true,          // demo; in prod, send confirmation email
            LastActiveAtUtc  = DateTime.UtcNow,
        };

        var createResult = await _users.CreateAsync(user, vm.Password);
        if (!createResult.Succeeded)
        {
            foreach (var e in createResult.Errors)
                ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        // Ensure the Student role exists, then assign it
        if (!await _roles.RoleExistsAsync("Student"))
            await _roles.CreateAsync(new ApplicationRole { Name = "Student" });

        await _users.AddToRoleAsync(user, "Student");
        await _signIn.SignInAsync(user, isPersistent: true);
        return LocalRedirect(returnUrl ?? "/");
    }

    // ── Logout ─────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // ── Access denied ──────────────────────────────────────────────────────

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();
}

// ── View models ────────────────────────────────────────────────────────────

public class LoginVm
{
    [Required, EmailAddress]
    public string Email    { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; }
}

public class RegisterVm
{
    [Required]
    public string FullName { get; set; } = "";

    [Required, EmailAddress]
    public string Email    { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}
