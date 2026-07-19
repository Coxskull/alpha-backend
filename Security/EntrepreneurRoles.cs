using System;
using System.Collections.Generic;

namespace Alpha.API.Security;

public static class EntrepreneurRoles
{
    public const string Driver = "driver";
    public const string Mechanic = "mechanic";
    public const string Supplier = "supplier";
    public const string Customer = "customer";
    public const string CommunityBuilder = "community_builder";

    public static readonly HashSet<string> PublicRegistrationRoles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            Driver,
            Mechanic,
            Supplier,
            Customer,
            CommunityBuilder
        };

    public static readonly HashSet<string> OperationalRoles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            Driver,
            Mechanic,
            Supplier,
            Customer
        };
}