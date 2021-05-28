using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MinecraftServerRCON;
using Newtonsoft.Json;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FreeHCWebsite.Controllers
{
    public class ShopController : Controller
    {

        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;
        public ShopController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [Route("sklep")]
        public IActionResult Index()
        {
            return View();
        }

        [Route("success")]
        public IActionResult Success()
        {
            return View();
        }

        [HttpPost]
        [Route("RedirectToPayment")]
        public IActionResult RedirectToPayment(int fhcoins, string nickname)
        {

            //walidacja formularza
            if (fhcoins == 0 || nickname == null)
            {
                return Json(new { message = "Musisz wypełnić wszystkie pola!" });
            }

            //sprawdz czy gracz jest na serwerze
            string playerGuid;
            try
            {
                var json = new WebClient().DownloadString("http://127.0.0.1:7284/onlinePlayers");
                List<string[]> players = JsonConvert.DeserializeObject<List<string[]>>(json);
                if (!players.Any(x => x[1].ToLower() == nickname.ToLower()))
                {
                    return Json(new { message = $"{nickname} nie jest obecnie na serwerze!" });
                }
                playerGuid = players.Where(x => x[1].ToLower() == nickname.ToLower()).First()[0];
            }
            catch
            {
                _logger.LogError("FHCoins plugin connection problem");
                return Json(new { message = "Serwer jest offline" });
            }

            //oblicz kwote do zaplaty
            double amount = fhcoins * 0.006;
            if (fhcoins >= 3000)
            {
                amount = fhcoins * 0.005;
            }
            //platnosc przelewem

            StripeConfiguration.ApiKey = "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";
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string>
                {
                    "p24"
                },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = Convert.ToInt64(amount*100),
                            Currency = "pln",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Punkty premium na serwerze FreeHC.pl",
                            },
                        },
                        Quantity = 1,
                    },
                },
                AllowPromotionCodes = true,
                Metadata = new Dictionary<string, string>
                {
                    {
                        "nickname", nickname
                    },
                    {
                        "playerUUID", playerGuid
                    },
                    {
                        "fhcoins", fhcoins.ToString()
                    }
                },
                Locale = "pl",
                Mode = "payment",
                SuccessUrl = "https://freehc.pl/success",
                CancelUrl = "https://freehc.pl/"
            };

            var service = new SessionService();
            var session = service.Create(options);
            return Json(new { id = session.Id });
        }

        [HttpPost]
        [Route("PaymentCallback")]
        public async Task<IActionResult> PaymentCallback(bool status, string email, string control, string userid)
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                  json,
                  Request.Headers["Stripe-Signature"],
                  secret: "whsec_OawSdjDwG3siYZ7Rhw0Uv9zxHSAzhepn",
                  throwOnApiVersionMismatch: false
                );
                if (stripeEvent.Type == Events.CheckoutSessionCompleted)
                {
                    Stripe.Checkout.Session checkoutSession = stripeEvent.Data.Object as Stripe.Checkout.Session;
                    if(checkoutSession.PaymentStatus == "paid")
                    {
                        string playerUUID = checkoutSession.Metadata["playerUUID"];
                        string fhcoins = checkoutSession.Metadata["fhcoins"];
                        new WebClient().DownloadString("http://127.0.0.1:7284/AddFhCoinsToPlayer?playerUUID="+playerUUID+"&fhcoins="+fhcoins);
                        Console.WriteLine($"Payment was successful for {checkoutSession.Metadata["playerUUID"]} ({checkoutSession.Metadata["nickname"]}) - {fhcoins}");
                    }
                }

                return Ok();
            }
            catch (StripeException e)
            {
                Console.WriteLine(e);
                return BadRequest();
            }
        }


    }
}

