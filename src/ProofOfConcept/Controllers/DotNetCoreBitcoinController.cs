using Microsoft.AspNetCore.Mvc;

namespace ProofOfConcept.Controllers
{
    public class DotNetCoreBitcoinController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
