using Microsoft.AspNetCore.Mvc;

namespace ProofOfConcept.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
