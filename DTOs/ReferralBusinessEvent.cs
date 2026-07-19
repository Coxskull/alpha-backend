using System;

namespace Alpha.API.DTOs;

public sealed record ReferralBusinessEvent(
    string EventKey,
    Guid SourceUserId,
    string SourceRole,
    Guid? OrderId,
    Guid? ServiceRequestId,
    Guid? PaymentId,
    string TransactionType,
    decimal EligibleAmount,
    string Currency,
    string Description
);