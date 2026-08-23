using System.ComponentModel.DataAnnotations;

namespace FeedCraft.Web.Models;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    /// Ticked, the new account also gets the Dealer role, which is what unlocks "My Listings".
    /// Left unticked it is a plain signed-in user with no role — a state worth being able to
    /// create, because it is what demonstrates [Authorize(Roles = "Dealer")] actually denying
    /// someone rather than merely being present in the source.
    /// </summary>
    [Display(Name = "I sell feed ingredients — register me as a dealer")]
    public bool IsDealer { get; set; } = true;
}
