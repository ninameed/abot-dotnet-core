using System;
using System.Net;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
namespace Abot.SiteSimulator.Controllers
{
    public class HttpResponseController : Controller
    {
        public IActionResult Index()
        {
            return Status200();
        }

        public IActionResult Status200()
        {
            Thread.Sleep(100);
            ViewBag.Header = "Status 200";
            ViewBag.Description = "This is a status 200 page";
            return View("BlankPage");
        }

        public IActionResult Status403()
        {
            Thread.Sleep(200);
            return StatusCode(403);
        }

        public IActionResult Status404()
        {
            Thread.Sleep(300);
            return NotFound();
        }

        public IActionResult Status500()
        {
            Thread.Sleep(400);
            return StatusCode(500);
        }

        public IActionResult Status503()
        {
            return StatusCode(503);
        }

        public IActionResult Redirect(int redirectHttpStatus, int destinationHttpStatus)
        {
            if(!IsValidRedirectStatus(redirectHttpStatus))
                throw new ArgumentException("redirectHttpStatus is invalid");

            if (!IsValidDestinationStatus(destinationHttpStatus))
                throw new ArgumentException("destinationHttpStatus is invalid");

            return new RedirectResult("/HttpResponse/Status" + destinationHttpStatus, (redirectHttpStatus == 301));
        }

        private bool IsValidRedirectStatus(int status)
        {
            switch (status)
            {
                case 301:
                case 302:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsValidDestinationStatus(int status)
        {
            switch (status)
            {
                case 200:
                case 403:
                case 404:
                case 500:
                case 503:
                    return true;
                default:
                    return false;
            }
        }
    }
}
