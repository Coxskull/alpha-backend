using Alpha.API.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class MemberBusinessActivityService
{
    private readonly AppDbContext _context;

    public MemberBusinessActivityService(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task RecordCustomerPurchaseAsync(
        Guid userId,
        Guid orderId,
        decimal transactionValue,
        CancellationToken cancellationToken = default)
    {
        await EnsureActivityRecordExistsAsync(
            userId,
            cancellationToken
        );

        var now = DateTime.UtcNow;

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE member_business_activity
            SET
                completed_orders =
                    completed_orders + 1,

                customer_purchases =
                    customer_purchases + 1,

                gross_transaction_value =
                    gross_transaction_value + {transactionValue},

                first_business_activity_at =
                    COALESCE(
                        first_business_activity_at,
                        {now}
                    ),

                last_business_activity_at = {now},
                is_business_active = TRUE,
                updated_at = {now}
            WHERE user_id = {userId};
            """,
            cancellationToken
        );
    }

    public async Task RecordDriverDeliveryAsync(
        Guid userId,
        Guid orderId,
        decimal transactionValue,
        CancellationToken cancellationToken = default)
    {
        await EnsureActivityRecordExistsAsync(
            userId,
            cancellationToken
        );

        var now = DateTime.UtcNow;

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE member_business_activity
            SET
                completed_orders =
                    completed_orders + 1,

                completed_deliveries =
                    completed_deliveries + 1,

                gross_transaction_value =
                    gross_transaction_value + {transactionValue},

                first_business_activity_at =
                    COALESCE(
                        first_business_activity_at,
                        {now}
                    ),

                last_business_activity_at = {now},
                is_business_active = TRUE,
                updated_at = {now}
            WHERE user_id = {userId};
            """,
            cancellationToken
        );
    }

    public async Task RecordSupplierFulfillmentAsync(
        Guid userId,
        Guid orderId,
        decimal transactionValue,
        CancellationToken cancellationToken = default)
    {
        await EnsureActivityRecordExistsAsync(
            userId,
            cancellationToken
        );

        var now = DateTime.UtcNow;

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE member_business_activity
            SET
                completed_orders =
                    completed_orders + 1,

                fulfilled_parts_orders =
                    fulfilled_parts_orders + 1,

                gross_transaction_value =
                    gross_transaction_value + {transactionValue},

                first_business_activity_at =
                    COALESCE(
                        first_business_activity_at,
                        {now}
                    ),

                last_business_activity_at = {now},
                is_business_active = TRUE,
                updated_at = {now}
            WHERE user_id = {userId};
            """,
            cancellationToken
        );
    }

    public async Task RecordMechanicServiceAsync(
        Guid userId,
        Guid serviceRequestId,
        decimal transactionValue,
        CancellationToken cancellationToken = default)
    {
        await EnsureActivityRecordExistsAsync(
            userId,
            cancellationToken
        );

        var now = DateTime.UtcNow;

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE member_business_activity
            SET
                completed_service_requests =
                    completed_service_requests + 1,

                gross_transaction_value =
                    gross_transaction_value + {transactionValue},

                first_business_activity_at =
                    COALESCE(
                        first_business_activity_at,
                        {now}
                    ),

                last_business_activity_at = {now},
                is_business_active = TRUE,
                updated_at = {now}
            WHERE user_id = {userId};
            """,
            cancellationToken
        );
    }

    public async Task SetInactiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureActivityRecordExistsAsync(
            userId,
            cancellationToken
        );

        var now = DateTime.UtcNow;

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE member_business_activity
            SET
                is_business_active = FALSE,
                updated_at = {now}
            WHERE user_id = {userId};
            """,
            cancellationToken
        );
    }

    public async Task RefreshBusinessActiveStatusAsync(
        Guid userId,
        int activeWithinDays = 90,
        CancellationToken cancellationToken = default)
    {
        await EnsureActivityRecordExistsAsync(
            userId,
            cancellationToken
        );

        var cutoffDate =
            DateTime.UtcNow.AddDays(-activeWithinDays);

        var now = DateTime.UtcNow;

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE member_business_activity
            SET
                is_business_active =
                    CASE
                        WHEN last_business_activity_at IS NOT NULL
                         AND last_business_activity_at >= {cutoffDate}
                        THEN TRUE
                        ELSE FALSE
                    END,

                updated_at = {now}
            WHERE user_id = {userId};
            """,
            cancellationToken
        );
    }

    private async Task EnsureActivityRecordExistsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO member_business_activity (
                id,
                user_id,
                completed_orders,
                completed_service_requests,
                completed_deliveries,
                fulfilled_parts_orders,
                customer_purchases,
                gross_transaction_value,
                first_business_activity_at,
                last_business_activity_at,
                is_business_active,
                updated_at
            )
            VALUES (
                {Guid.NewGuid()},
                {userId},
                0,
                0,
                0,
                0,
                0,
                0,
                NULL,
                NULL,
                FALSE,
                {now}
            )
            ON CONFLICT (user_id)
            DO NOTHING;
            """,
            cancellationToken
        );
    }
}