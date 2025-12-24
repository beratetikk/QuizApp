using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SoruDeneme.Filters
{
    public class RequireRoleAttribute : ActionFilterAttribute
    {
        private readonly string _role;

        public RequireRoleAttribute(string role) => _role = role;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var role = context.HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrWhiteSpace(role))
            {
                context.Result = new RedirectToActionResult("Index", "Login", null);
                return;
            }

            if (!string.Equals(role, _role, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new ForbidResult();
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
