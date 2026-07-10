using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ANU_Admissions.Models;
using ANU_Admissions.Resources;
using ANU_Admissions.Services;
using ANU_Admissions.ViewModels;

namespace ANU_Admissions.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAppEmailSender _emailSender;
    private readonly IStringLocalizer<SharedResource> _l;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAppEmailSender emailSender,
        IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
        _l = localizer;
    }

    [HttpGet]
    public IActionResult Register()
    {
        var already = RedirectIfAuthenticated();
        if (already != null) return already;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Rule: a student mobile may belong to ONE account only.
            var studentPhone = model.PhoneNumber?.Trim();
            if (!string.IsNullOrEmpty(studentPhone) &&
                await _userManager.Users.AnyAsync(u => u.PhoneNumber == studentPhone))
            {
                ModelState.AddModelError(nameof(model.PhoneNumber),
                    _l["Acc_StudentPhoneTaken"].Value);
                return View(model);
            }

            // Rule: a parent mobile may be shared by AT MOST 3 accounts (siblings).
            var parentPhone = model.ParentPhoneNumber?.Trim();
            if (!string.IsNullOrEmpty(parentPhone) &&
                await _userManager.Users.CountAsync(u => u.ParentPhoneNumber == parentPhone) >= 3)
            {
                ModelState.AddModelError(nameof(model.ParentPhoneNumber),
                    _l["Acc_ParentPhoneMaxed"].Value);
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                ParentPhoneNumber = model.ParentPhoneNumber
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // Assign Student role
                await _userManager.AddToRoleAsync(user, "Student");

                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Dashboard", "Student");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(error.Code));
            }
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        var already = RedirectIfAuthenticated();
        if (already != null) return already;
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, _l["Acc_AccountLocked"].Value);
                return View(model);
            }

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                var roles = await _userManager.GetRolesAsync(user!);

                // Redirect based on role
                if (roles.Contains("Admin"))
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
                else if (roles.Contains("Student"))
                {
                    return RedirectToAction("Dashboard", "Student");
                }

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, _l["Acc_InvalidCredentials"].Value);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // ================================
    // FORGOT / RESET PASSWORD
    // ================================

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        var already = RedirectIfAuthenticated();
        if (already != null) return already;
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Look the user up, but NEVER reveal whether the email exists: always
        // redirect to the same generic confirmation page.
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null)
        {
            var rawToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

            var resetLink = Url.Action(
                "ResetPassword", "Account",
                new { email = model.Email, token = encodedToken },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                model.Email,
                _l["Acc_ResetEmailSubject"].Value,
                $"{_l["Acc_ResetEmailBody"].Value}\n{resetLink}");
        }

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ResetPassword(string? email = null, string? token = null)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            // Missing/invalid link — don't expose details.
            TempData["Error"] = _l["Acc_ResetLinkInvalid"].Value;
            return RedirectToAction(nameof(Login));
        }

        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Don't reveal whether the email exists: if no user, show the same
        // success page as a valid reset would.
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        string rawToken;
        try
        {
            rawToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
        }
        catch
        {
            ModelState.AddModelError(string.Empty, _l["Acc_ResetLinkInvalidShort"].Value);
            return View(model);
        }

        var result = await _userManager.ResetPasswordAsync(user, rawToken, model.NewPassword);
        if (result.Succeeded)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, TranslateIdentityError(error.Code));
        }
        return View(model);
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }

    // If the request is already authenticated, send the user to their role's
    // home so the public auth pages (Login / Register / ForgotPassword) never
    // render for a signed-in user. Returns null when the request is anonymous
    // so the caller renders the view normally.
    private IActionResult? RedirectIfAuthenticated()
    {
        if (User.Identity?.IsAuthenticated != true) return null;
        if (User.IsInRole("Admin"))   return RedirectToAction("Dashboard", "Admin");
        if (User.IsInRole("Student")) return RedirectToAction("Dashboard", "Student");
        return RedirectToAction("Index", "Home");
    }

    private string TranslateIdentityError(string code)
    {
        var key = code switch
        {
            "DuplicateUserName" or "DuplicateEmail" => "Id_DuplicateEmail",
            "InvalidEmail" => "Id_InvalidEmail",
            "PasswordTooShort" => "Id_PasswordTooShort",
            "PasswordRequiresDigit" => "Id_PasswordRequiresDigit",
            "PasswordRequiresUpper" => "Id_PasswordRequiresUpper",
            "PasswordRequiresLower" => "Id_PasswordRequiresLower",
            "PasswordRequiresNonAlphanumeric" => "Id_PasswordRequiresNonAlphanumeric",
            "InvalidToken" => "Id_InvalidToken",
            _ => "Id_Generic"
        };
        return _l[key].Value;
    }
}
