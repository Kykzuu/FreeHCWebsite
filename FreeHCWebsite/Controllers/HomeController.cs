using FreeHCWebsite.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace FreeHCWebsite.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        [Route("/")]
        [Route("index")]
        public IActionResult Index()
        {
            ViewData["online"] = 0;
            try
            {
                var json = new WebClient().DownloadString("http://127.0.0.1:7284/onlinePlayers");
                List<string[]> players = JsonConvert.DeserializeObject<List<string[]>>(json);
                ViewData["online"] = players.Count();
            }
            catch
            {
                _logger.LogError("FHCoins plugin connection problem");
            }
            return View();
        }

    }
}
