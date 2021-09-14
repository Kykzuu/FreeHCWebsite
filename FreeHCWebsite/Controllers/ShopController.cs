using CoreRCON;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        public IActionResult RedirectToPayment(string item, string nickname)
        {

            //walidacja formularza
            if (item == null || nickname == null)
            {
                return Json(new { message = "Musisz wypełnić wszystkie pola!" });
            }

            int toPay = 0;
            List<string> commands = new List<string>();
            if(item == "vip")
            {
                toPay = 1000; //10 zl
                commands.Add($"bc &3&lGracz &5&l{nickname} &3&lzakupił &5&lVIPa! &3&lDziękujemy! <3");
                commands.Add($"bc &3Sklep: &5https://shinimc.pl/sklep");
                commands.Add($"lp user {nickname} parent add vip");
            }

            if (item == "sponsor")
            {
                toPay = 2500; //25 zl
                commands.Add($"bc &3&lGracz &5&l{nickname} &3&lzakupił &5&lSPONSORA! &3&lDziękujemy! <3");
                commands.Add($"bc &3Sklep: &5https://shinimc.pl/sklep");
                commands.Add($"lp user {nickname} parent add sponsor");
            }

            if (item == "25premiumkey")
            {
                toPay = 1200; //12 zl
                commands.Add($"bc &3&lGracz &5&l{nickname} &3&lzakupił &5&l25 PremiumKey! &3&lDziękujemy! <3");
                commands.Add($"bc &3Sklep: &5https://shinimc.pl/sklep");
                commands.Add($"goldencrates givekey {nickname} premiumkey 25");
            }

            if (item == "25ultrakey")
            {
                toPay = 1800; //18 zl
                commands.Add($"bc &3&lGracz &5&l{nickname} &3&lzakupił &5&l25 UltraKey! &3&lDziękujemy! <3");
                commands.Add($"bc &3Sklep: &5https://shinimc.pl/sklep");
                commands.Add($"goldencrates givekey {nickname} ultrakey 25");
            }

            if (item == "50premiumkey")
            {
                toPay = 2040; //20,4 zl
                commands.Add($"bc &3&lGracz &5&l{nickname} &3&lzakupił &5&l50 PremiumKey! &3&lDziękujemy! <3");
                commands.Add($"bc &3Sklep: &5https://shinimc.pl/sklep");
                commands.Add($"goldencrates givekey {nickname} premiumkey 50");
            }

            if (item == "50ultrakey")
            {
                toPay = 3060; //30,60 zl
                commands.Add($"bc &3&lGracz &5&l{nickname} &3&lzakupił &5&l50 UltraKey! &3&lDziękujemy! <3");
                commands.Add($"bc &3Sklep: &5https://shinimc.pl/sklep");
                commands.Add($"goldencrates givekey {nickname} ultrakey 50");
            }

            string jsonCommands = JsonConvert.SerializeObject(commands, Formatting.Indented);

            //platnosc przelewem
            //test: sk_test_51HKZoPDivRgf8jpg8TE7nlDAz1omrIeI1iIFyWRSNd37f3YwLcrnl5CAmXvib62bYpA7AGEuv2WtKgYZeatE35tb00acr2oj9n
            //live: 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
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
                            UnitAmount = toPay, //do zaplaty
                            Currency = "pln",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Przedmioty i rangi premium na serwerze ShiniMC.pl",
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
                        "commands", jsonCommands
                    }
                },
                Locale = "pl",
                Mode = "payment",
                SuccessUrl = "https://shinimc.pl/success",
                CancelUrl = "https://shinimc.pl/"
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
                  secret: "whsec_OawSdjDwG3siYZ7Rhw0Uv9zxHSAzhepn", //whsec_OawSdjDwG3siYZ7Rhw0Uv9zxHSAzhepn
                  throwOnApiVersionMismatch: false
                );
                if (stripeEvent.Type == Events.CheckoutSessionCompleted)
                {
                    Stripe.Checkout.Session checkoutSession = stripeEvent.Data.Object as Stripe.Checkout.Session;
                    if(checkoutSession.PaymentStatus == "paid")
                    {
                        //ZAPLACONO
                        var rcon = new RCON(IPAddress.Parse("152.70.57.201"), 25575, "M5YwhJBbn3EVG24r5qKg");
                        rcon.ConnectAsync().Wait();

                        string jsonCommands = checkoutSession.Metadata["commands"];
                        List<string> commands = JsonConvert.DeserializeObject<List<string>>(jsonCommands);
                        foreach(string cmd in commands)
                        {
                            Console.WriteLine(cmd);
                            rcon.SendCommandAsync(cmd).Wait();
                        }
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

