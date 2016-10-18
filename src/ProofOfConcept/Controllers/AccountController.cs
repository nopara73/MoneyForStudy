using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using ProofOfConcept.Models;
using ProofOfConcept.Models.AccountViewModels;
using ProofOfConcept.Services;
using ProofOfConcept.Utilities;

namespace ProofOfConcept.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ISmsSender _smsSender;
        private readonly ILogger _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ISmsSender smsSender,
            ILoggerFactory loggerFactory)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _smsSender = smsSender;
            _logger = loggerFactory.CreateLogger<AccountController>();
        }

        //
        // GET: /Account/LoginRegister
        [HttpGet]
        [AllowAnonymous]
        public IActionResult LoginRegister(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        //
        // POST: /Account/LoginRegister
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginRegister(LoginRegisterViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var isEmail = new RegexUtilities().IsValidEmail(model.UsernameEmail);
                string email;
                string username;
                var password = model.Password;
                ApplicationUser user;

                if(isEmail)
                {
                    // Determine if loginattempt, if so try login
                    email = model.UsernameEmail;
                    if(_userManager.Users.Any(x => x.Email == email))
                    {
                        user = _userManager.Users.FirstOrDefault(x => x.Email == email);
                        username = user.UserName;
                        var result = await _signInManager.PasswordSignInAsync(username, password, isPersistent: true, lockoutOnFailure: false);
                        if (result.Succeeded)
                        {
                            _logger.LogInformation(1, "User logged in.");
                            return RedirectToLocal(returnUrl);
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                        }
                    }
                    // Else try register
                    else
                    {
                        username = GetUniqueUsername(_userManager.Users);
                        user = new ApplicationUser { UserName = username, Email = email };
                        var result = await _userManager.CreateAsync(user, password);
                        if (result.Succeeded)
                        {
                            await _signInManager.SignInAsync(user, isPersistent: true);
                            _logger.LogInformation(3, "User created a new account with password.");
                            return RedirectToLocal(returnUrl);
                        }
                        AddErrors(result);
                    }
                }
                else
                {
                    // Determine if loginattempt, if so try login
                    username = model.UsernameEmail;
                    if(_userManager.Users.Any(x => x.UserName == username))
                    {
                        user = _userManager.Users.FirstOrDefault(x => x.UserName == username);
                        email = user.Email;
                        var result = await _signInManager.PasswordSignInAsync(username, model.Password, isPersistent: true, lockoutOnFailure: false);
                        if (result.Succeeded)
                        {
                            _logger.LogInformation(1, "User logged in.");
                            return RedirectToLocal(returnUrl);
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                        }
                    }
                    // Else try register
                    else
                    {
                        email = "";
                        user = new ApplicationUser { UserName = username, Email = email };
                        var result = await _userManager.CreateAsync(user, password);
                        if (result.Succeeded)
                        {
                            await _signInManager.SignInAsync(user, isPersistent: true);
                            _logger.LogInformation(3, "User created a new account with password.");
                            return RedirectToLocal(returnUrl);
                        }
                        AddErrors(result);
                    }
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        private string GetUniqueUsername(IEnumerable<ApplicationUser> users)
        {
            var username = StringUtilities.GetRandomLetters(7);
            foreach(var u in users)
            {
                if (u.UserName == username)
                    username = GetUniqueUsername(users);
            }
            return username;
        }

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogOff()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation(4, "User logged out.");
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        // GET: /Account/ConfirmEmail
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return View("Error");
            }
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return View("Error");
            }
            var result = await _userManager.ConfirmEmailAsync(user, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        //
        // GET: /Account/ResetPassword
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string code = null)
        {
            return code == null ? View("Error") : View();
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await _userManager.FindByNameAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction(nameof(AccountController.ResetPasswordConfirmation), "Account");
            }
            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(AccountController.ResetPasswordConfirmation), "Account");
            }
            AddErrors(result);
            return View();
        }

        //
        // GET: /Account/ResetPasswordConfirmation
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }        

        #region Helpers

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private Task<ApplicationUser> GetCurrentUserAsync()
        {
            return _userManager.GetUserAsync(HttpContext.User);
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
        }

        #endregion
    }
}
