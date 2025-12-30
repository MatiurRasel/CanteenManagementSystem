//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Filters;
//using CanteenManagementSystem.Services;

//namespace CanteenManagementSystem.Filters
//{
//    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
//    public class OperatorAuthorizeAttribute : Attribute, IAsyncActionFilter
//    {
//        private readonly string _permission;

//        public OperatorAuthorizeAttribute(string permission = null)
//        {
//            _permission = permission;
//        }

//        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
//        {
//            var authService = context.HttpContext.RequestServices.GetService<OperatorAuthService>();

//            if (authService == null || !authService.IsAuthenticated())
//            {
//                context.Result = new RedirectToActionResult("Login", "Operator", new { returnUrl = context.HttpContext.Request.Path });
//                return;
//            }

//            if (!string.IsNullOrEmpty(_permission) && !authService.HasPermission(_permission))
//            {
//                context.Result = new ViewResult { ViewName = "AccessDenied" };
//                return;
//            }

//            await next();
//        }
//    }
//}