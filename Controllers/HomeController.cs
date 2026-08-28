using Microsoft.AspNetCore.Mvc;

namespace FeedCraft.Web.Controllers
{
    /// <summary>
    /// The app's front door and its About page.
    ///
    /// This is deliberately not a restore of the scaffold HomeController that was deleted in the
    /// Step 11 cleanup. That one existed only to hold a stub Index, a Privacy page nothing linked
    /// to, and an Error action the exception handler pointed at; all three were dead weight. The
    /// error pipeline still points at <see cref="ErrorController"/> and nothing here touches it.
    ///
    /// Both actions are static content, so this controller takes no dependencies at all. In
    /// particular it does not resolve the optimizer or the database: the worked example on the
    /// landing page is written into the view, so the front page cannot be broken by a solver or a
    /// connection failure. /Feed remains the only surface that solves anything.
    /// </summary>
    public class HomeController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult About()
        {
            return View();
        }
    }
}
