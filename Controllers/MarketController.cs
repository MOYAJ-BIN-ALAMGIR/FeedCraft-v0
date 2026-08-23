using FeedCraft.Domain.Models;
using FeedCraft.Infrastructure.Data;
using FeedCraft.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace FeedCraft.Web.Controllers
{
    /// <summary>
    /// The public side of the marketplace. Deliberately has no [Authorize] attribute: a farmer
    /// comparing prices should never be asked to make an account.
    ///
    /// Only the listing's own ContactInfo is exposed — never the dealer's account email. That
    /// keeps registration addresses private and means this page needs no join to AspNetUsers.
    /// </summary>
    public class MarketController : Controller
    {
        private readonly FeedCraftDbContext _db;

        public MarketController(FeedCraftDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Index(string? q)
        {
            var term = q?.Trim();
            var hasQuery = !string.IsNullOrEmpty(term);

            IQueryable<DealerListing> query = _db.DealerListings.AsNoTracking();

            if (hasQuery)
            {
                // EF.Functions.Like compiles to SQL LIKE, which SQLite treats as
                // case-insensitive for ASCII. The obvious-looking l.IngredientName.Contains(term)
                // would compile to instr(), which is case *sensitive* — searching "corn" would
                // then fail to find a listing named "Corn".
                query = query.Where(l => EF.Functions.Like(l.IngredientName, "%" + term + "%"));
            }

            return View(new MarketSearchViewModel
            {
                Query = term,
                HasQuery = hasQuery,

                // Cheapest first — the entire point of the page. Name breaks ties so the order
                // is stable rather than whatever SQLite happens to return.
                Results = query
                    .OrderBy(l => l.Price)
                    .ThenBy(l => l.IngredientName)
                    .ToList()
            });
        }
    }
}
