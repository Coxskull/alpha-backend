using System.Security.Claims;
using Alpha.API.Models;
namespace Alpha.API.Security;

public static class OrderAccess
{
    public static bool IsStaff(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        && (user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.Dispatcher));

    public static bool CanPay(ClaimsPrincipal user, Order order) => IsStaff(user)
        || (user.Identity?.IsAuthenticated == true && user.IsInRole(AppRoles.Customer)
            && Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            && order.CustomerId == id);

    public static bool CanAccessCustomer(ClaimsPrincipal user, Guid customerId) => IsStaff(user)
        || (user.Identity?.IsAuthenticated == true
            && Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id == customerId);
}
