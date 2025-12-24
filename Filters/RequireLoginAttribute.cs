using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SoruDeneme.Filters
{
    public class RequireLoginAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var userId = context.HttpContext.Session.GetInt32("UserId");
            var role = context.HttpContext.Session.GetString("UserRole");

            if (userId == null || string.IsNullOrWhiteSpace(role))
            {
                context.Result = new RedirectToActionResult("Index", "Login", null);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
