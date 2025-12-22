using Instagram.Services;
using Microsoft.AspNetCore.Mvc.Filters;

public class DbCountFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        DbCallInterceptor.Reset();  // Reset for each API request
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        var controller = context.RouteData.Values["controller"]?.ToString();
        var action = context.RouteData.Values["action"]?.ToString();

        context.HttpContext.Response.Headers.Add("X-Controller", controller);
        context.HttpContext.Response.Headers.Add("X-Action", action);
        context.HttpContext.Response.Headers.Add("X-DB-Calls", DbCallInterceptor.DbCallCount.ToString());
    }
}
