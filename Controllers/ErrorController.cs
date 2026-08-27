using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FeedCraft.Web.Models;

namespace FeedCraft.Web.Controllers
{
    /// <summary>
    /// The last-resort page for an unhandled exception outside Development, wired up by
    /// <c>app.UseExceptionHandler("/Error")</c> in Program.cs.
    ///
    /// It exists as its own controller rather than as an action on a general-purpose one so that
    /// nothing else has to stay alive just to keep the error pipeline pointing somewhere real.
    /// The default route resolves /Error to this Index, so no route attribute is needed.
    /// </summary>
    public class ErrorController : Controller
    {
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Index()
        {
            // The view name is spelled out on purpose. The page lives at Views/Shared/Error.cshtml
            // — the framework-wide convention, and where a status-code page would also look for it
            // — whereas a bare View() would search for Views/Error/Index.cshtml and find nothing.
            //
            // The trace identifier is what correlates this page with the server log entry; it is
            // the only thing shown, because the exception detail itself must not reach the browser.
            return View("Error", new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
