using FeedCraft.Infrastructure.Data;
using FeedCraft.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace FeedCraft.Web.Controllers
{
    /// <summary>
    /// Register / log in / log out, hand-written rather than scaffolded from
    /// Microsoft.AspNetCore.Identity.UI so the app stays pure MVC and there is no Razor Pages
    /// machinery to explain.
    ///
    /// Nothing here is [Authorize]d and no global authorization filter is registered, so the
    /// rest of the app remains anonymous by default — only ListingsController opts in.
    /// </summary>
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Email doubles as the user name, which is why Login signs in with the email.
            var user = new IdentityUser { UserName = model.Email, Email = model.Email };
            var created = await _userManager.CreateAsync(user, model.Password);

            if (!created.Succeeded)
            {
                AddErrors(created);
                return View(model);
            }

            if (model.IsDealer)
            {
                var granted = await _userManager.AddToRoleAsync(user, FeedCraftDbContext.DealerRole);

                if (!granted.Succeeded)
                {
                    // Don't leave a half-built account behind: they could sign in but would be
                    // denied the very page they registered for. Roll the user back instead.
                    await _userManager.DeleteAsync(user);
                    AddErrors(granted);
                    return View(model);
                }
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

            if (IsSafeReturnUrl(returnUrl))
            {
                return LocalRedirect(returnUrl!);
            }

            return model.IsDealer
                ? RedirectToAction("Index", "Listings")
                : RedirectToAction("Index", "Market");
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                // One message for both "no such account" and "wrong password". Distinguishing
                // them would let anyone probe which email addresses are registered here.
                ModelState.AddModelError(string.Empty, result.IsLockedOut
                    ? "This account is temporarily locked after too many failed attempts. Try again shortly."
                    : "Incorrect email or password.");

                return View(model);
            }

            if (IsSafeReturnUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl!);
            }

            return RedirectToAction("Index", "Feed");
        }

        /// <summary>POST-only: signing out is a state change, so it carries an antiforgery token.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Feed");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        /// <summary>
        /// Guards against an open redirect: without the IsLocalUrl check, a crafted
        /// ?returnUrl=https://evil.example link would bounce a freshly authenticated user
        /// off-site.
        /// </summary>
        private bool IsSafeReturnUrl(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl);

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
