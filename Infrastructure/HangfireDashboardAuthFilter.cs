using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace Denkiishi_v2.Infrastructure;

public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User?.Identity?.IsAuthenticated == true
               && httpContext.User.IsInRole("Admin");
    }
}

