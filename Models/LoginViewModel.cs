using System.ComponentModel.DataAnnotations;

namespace FeedCraft.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    /// <summary>
    /// Where to land after signing in — set by the cookie middleware when it bounced an
    /// anonymous request. Always run through Url.IsLocalUrl before redirecting to it.
    /// </summary>
    public string? ReturnUrl { get; set; }
}
