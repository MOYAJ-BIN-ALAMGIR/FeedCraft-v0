using FeedCraft.Domain.Models;
using FeedCraft.Infrastructure.Data;
using FeedCraft.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace FeedCraft.Web.Controllers
{
    /// <summary>
    /// A dealer's own listings. Structured like IngredientsController (antiforgery on every POST,
    /// TempData message, post-redirect-get back to Index), with two differences that matter:
    ///
    /// 1. The whole controller is role-gated, so this is the app's only authorization boundary.
    /// 2. Every read and write is scoped to the signed-in dealer. See <see cref="FindOwned"/> —
    ///    ownership is part of the query, not a check bolted on afterwards.
    /// </summary>
    [Authorize(Roles = FeedCraftDbContext.DealerRole)]
    public class ListingsController : Controller
    {
        private readonly FeedCraftDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public ListingsController(FeedCraftDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var userId = CurrentUserId();

            var listings = _db.DealerListings
                .AsNoTracking()
                .Where(l => l.DealerUserId == userId)
                .OrderBy(l => l.IngredientName)
                .ThenBy(l => l.Price)
                .ToList();

            return View(listings);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new ListingEditViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(ListingEditViewModel form)
        {
            if (!ModelState.IsValid)
            {
                return View(form);
            }

            // Field-by-field copy into a fresh entity, the same discipline
            // IngredientsController.Create uses. Note what is *not* copied: DealerUserId comes
            // from the signed-in principal, so a hand-crafted post cannot attribute a listing
            // to another dealer.
            var listing = new DealerListing
            {
                DealerUserId = CurrentUserId(),
                IngredientName = form.Listing.IngredientName.Trim(),
                Price = form.Listing.Price,
                ContactInfo = form.Listing.ContactInfo.Trim(),
                UpdatedAt = DateTime.Now
            };

            _db.DealerListings.Add(listing);
            _db.SaveChanges();

            TempData["ListingMessage"] =
                $"Listed \"{listing.IngredientName}\" at ${listing.Price:F2}/kg.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var listing = FindOwned(id);

            if (listing == null)
            {
                return NotFound();
            }

            return View(new ListingEditViewModel { Listing = listing, IsEdit = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, ListingEditViewModel form)
        {
            if (!ModelState.IsValid)
            {
                form.Listing.Id = id;
                form.IsEdit = true;
                return View(form);
            }

            var listing = FindOwned(id, tracked: true);

            if (listing == null)
            {
                return NotFound();
            }

            listing.IngredientName = form.Listing.IngredientName.Trim();
            listing.Price = form.Listing.Price;
            listing.ContactInfo = form.Listing.ContactInfo.Trim();
            listing.UpdatedAt = DateTime.Now;

            _db.SaveChanges();

            TempData["ListingMessage"] = $"Updated \"{listing.IngredientName}\".";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var listing = FindOwned(id, tracked: true);

            if (listing == null)
            {
                return NotFound();
            }

            var name = listing.IngredientName;
            _db.DealerListings.Remove(listing);
            _db.SaveChanges();

            TempData["ListingMessage"] = $"Removed \"{name}\" from the market.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// One listing belonging to the caller, or null.
        ///
        /// Ownership is expressed in the WHERE clause rather than fetched-then-compared, so no
        /// code path can load somebody else's row and forget to check it. Callers turn null into
        /// NotFound rather than Forbid: a 403 would confirm that the id exists and merely belongs
        /// to another dealer.
        /// </summary>
        private DealerListing? FindOwned(int id, bool tracked = false)
        {
            var userId = CurrentUserId();

            IQueryable<DealerListing> query = _db.DealerListings;

            if (!tracked)
            {
                query = query.AsNoTracking();
            }

            return query.FirstOrDefault(l => l.Id == id && l.DealerUserId == userId);
        }

        private string CurrentUserId() => _userManager.GetUserId(User) ?? string.Empty;
    }
}
