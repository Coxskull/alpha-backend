using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServiceRequestsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ServiceRequestsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> GetAll()
    {
        var requests = await _context.ServiceRequests
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(requests);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "admin,dispatcher,mechanic,driver,supplier,provider,customer")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var request = await _context.ServiceRequests.FindAsync(id);

        if (request == null)
            return NotFound();

        return Ok(request);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(CreateServiceRequestDto dto)
    {
        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            VehicleInfo = dto.VehicleInfo,
            IssueDescription = dto.IssueDescription,
            ServiceAddress = dto.ServiceAddress,
            Zone = dto.Zone,
            FinalAmount = dto.FinalAmount,
            PaymentStatus = "unpaid",
            Status = "new_request",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ServiceRequests.Add(request);
        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpGet("my-mechanic")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> GetMyMechanicRequests()
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Forbid();

        var mechanic = await _context.Mechanics
            .FirstOrDefaultAsync(x => x.UserId == userId.Value);

        if (mechanic == null)
            return Forbid();

        var requests = await _context.ServiceRequests
            .Where(x => x.MechanicId == mechanic.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(requests);
    }

    [HttpGet("my-driver")]
    [Authorize(Roles = "driver")]
    public async Task<IActionResult> GetMyDriverRequests()
    {
        var email = User.GetEmail();

        if (string.IsNullOrWhiteSpace(email))
            return Forbid();

        var driver = await _context.Drivers
            .FirstOrDefaultAsync(x => x.Email == email);

        if (driver == null)
            return Forbid();

        var requests = await _context.ServiceRequests
            .Where(x => x.DriverId == driver.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(requests);
    }

    [HttpPost("{id}/assign-provider")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> AssignProvider(Guid id)
    {
        var request = await _context.ServiceRequests.FindAsync(id);

        if (request == null)
            return NotFound();

        var provider = await _context.Suppliers
            .Where(x =>
                x.AvailabilityStatus == "available" &&
                x.Territory == request.Zone)
            .OrderBy(x => x.CurrentWorkload)
            .ThenByDescending(x => x.ResponseRate)
            .FirstOrDefaultAsync();

        if (provider == null)
        {
            request.Status = "provider_needed";
            request.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return BadRequest("No available provider found.");
        }

        request.ProviderId = provider.Id;
        request.Status = "provider_assigned";
        request.UpdatedAt = DateTime.UtcNow;

        provider.CurrentWorkload += 1;

        await _context.SaveChangesAsync();

        return Ok(new { request, provider });
    }

    [HttpPost("{id}/provider-accept")]
    [Authorize(Roles = "supplier,provider,admin,dispatcher")]
    public async Task<IActionResult> ProviderAccept(Guid id)
    {
        var request = await _context.ServiceRequests.FindAsync(id);

        if (request == null)
            return NotFound();

        request.Status = "provider_accepted";
        request.ProviderAcceptedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/assign-nearest-mechanic")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> AssignNearestMechanic(Guid id)
    {
        var request = await _context.ServiceRequests.FindAsync(id);

        if (request == null)
            return NotFound();

        var mechanic = await _context.Mechanics
            .Where(m =>
                m.AvailabilityStatus == "available" &&
                m.ServiceArea == request.Zone)
            .OrderBy(m => m.ActiveJobs)
            .ThenByDescending(m => m.ResponseRate)
            .FirstOrDefaultAsync();

        if (mechanic == null)
        {
            request.Status = "mechanic_needed";
            request.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return BadRequest("No available mechanic found.");
        }

        request.MechanicId = mechanic.Id;
        request.Status = "mechanic_assigned";
        request.UpdatedAt = DateTime.UtcNow;

        mechanic.AvailabilityStatus = "busy";
        mechanic.ActiveJobs += 1;

        await _context.SaveChangesAsync();

        return Ok(new { request, mechanic });
    }

    [HttpPost("{id}/mechanic-accept")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> MechanicAccept(Guid id)
    {
        var request = await GetOwnedMechanicRequest(id);

        if (request == null)
            return Forbid();

        request.Status = "mechanic_accepted";
        request.MechanicAcceptedAt = DateTime.UtcNow;
        request.AcceptedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/request-parts")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> RequestParts(Guid id, RequestPartsDto dto)
    {
        var request = await GetOwnedMechanicRequest(id);

        if (request == null)
            return Forbid();

        var partsRequest = new PartsRequest
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            MechanicId = request.MechanicId,
            PartDescription = dto.PartDescription,
            Notes = dto.Notes,
            Status = "requested",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.PartsRequests.Add(partsRequest);

        request.PartsRequestId = partsRequest.Id;
        request.PartsRequestNote = dto.Notes;
        request.Status = "parts_requested";
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { request, partsRequest });
    }

    [HttpPost("{id}/assign-driver")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> AssignDriver(Guid id)
    {
        var request = await _context.ServiceRequests.FindAsync(id);

        if (request == null)
            return NotFound();

        var driver = await _context.Drivers
            .Where(x =>
                x.AvailabilityStatus == "available" &&
                x.Territory == request.Zone)
            .OrderBy(x => x.ActiveJobs)
            .ThenByDescending(x => x.ResponseRate)
            .FirstOrDefaultAsync();

        if (driver == null)
        {
            request.Status = "driver_needed";
            request.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return BadRequest("No available driver found.");
        }

        request.DriverId = driver.Id;
        request.Status = "driver_assigned";
        request.DriverAssignedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        driver.AvailabilityStatus = "busy";
        driver.ActiveJobs += 1;

        await _context.SaveChangesAsync();

        return Ok(new { request, driver });
    }

    [HttpPost("{id}/driver-status")]
    [Authorize(Roles = "driver,admin,dispatcher")]
    public async Task<IActionResult> DriverStatus(Guid id, UpdateServiceStatusDto dto)
    {
        var allowedStatuses = new[]
        {
            "parts_picked_up",
            "parts_delivered"
        };

        if (!allowedStatuses.Contains(dto.Status))
            return BadRequest("Invalid driver status.");

        var request = await _context.ServiceRequests.FindAsync(id);

        if (request == null)
            return NotFound();

        request.Status = dto.Status;
        request.UpdatedAt = DateTime.UtcNow;

        if (dto.Status == "parts_delivered" && request.DriverId != null)
        {
            var driver = await _context.Drivers.FindAsync(request.DriverId.Value);

            if (driver != null)
            {
                driver.ActiveJobs = Math.Max(0, driver.ActiveJobs - 1);
                driver.AvailabilityStatus = "available";
            }
        }

        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/start-repair")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> StartRepair(Guid id)
    {
        var request = await GetOwnedMechanicRequest(id);

        if (request == null)
            return Forbid();

        request.Status = "repair_started";
        request.StartedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/proof")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> UploadProof(Guid id, UploadProofDto dto)
    {
        var request = await GetOwnedMechanicRequest(id);

        if (request == null)
            return Forbid();

        var proof = new RepairProof
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            MechanicId = request.MechanicId,
            ImageUrl = dto.ImageUrl,
            Notes = dto.Notes,
            UploadedAt = DateTime.UtcNow
        };

        _context.RepairProofs.Add(proof);

        request.ProofImageUrl = dto.ImageUrl;
        request.ProofUploadedAt = DateTime.UtcNow;
        request.Status = "proof_uploaded";
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { request, proof });
    }

    [HttpPost("{id}/complete")]
    [Authorize(Roles = "mechanic,admin,dispatcher")]
    public async Task<IActionResult> Complete(Guid id, CompleteServiceRequestDto dto)
    {
        var request = await _context.ServiceRequests.FindAsync(id);

        if (request == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.ProofImageUrl))
            return BadRequest("Proof of completion is required.");

        var finalAmount = dto.FinalAmount > 0
            ? dto.FinalAmount
            : request.FinalAmount;

        if (finalAmount <= 0)
            finalAmount = 100;

        var providerAmount = finalAmount * 0.20m;
        var mechanicAmount = finalAmount * 0.50m;
        var driverAmount = finalAmount * 0.15m;
        var alphaFee = finalAmount - providerAmount - mechanicAmount - driverAmount;

        request.FinalAmount = finalAmount;
        request.Status = "completed";
        request.PaymentStatus = "paid";
        request.CompletedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        var financial = new OrderFinancial
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = request.Id,
            Currency = "USD",
            ExchangeRate = 1,
            ItemSubtotal = finalAmount,
            DeliveryFee = 0,
            ServiceFee = 0,
            Tax = 0,
            Discount = 0,
            TotalAmount = finalAmount,
            CustomerPaid = finalAmount,
            SupplierAmount = providerAmount,
            MechanicAmount = mechanicAmount,
            DriverAmount = driverAmount,
            SupplierEarning = providerAmount,
            DriverEarning = driverAmount,
            AlphaPlatformFee = alphaFee,
            CompanyRevenue = alphaFee,
            FinancialStatus = "pending_review",
            PayoutStatus = "manual_review",
            CompletionProofUrl = request.ProofImageUrl,
            CreatedAt = DateTime.UtcNow
        };

        _context.OrderFinancials.Add(financial);

        var mechanic = request.MechanicId == null
            ? null
            : await _context.Mechanics.FindAsync(request.MechanicId.Value);

        if (mechanic != null)
        {
            mechanic.ActiveJobs = Math.Max(0, mechanic.ActiveJobs - 1);
            mechanic.AvailabilityStatus = "available";
        }

        await _context.SaveChangesAsync();

        return Ok(new { request, financial });
    }

    private async Task<ServiceRequest?> GetOwnedMechanicRequest(Guid requestId)
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Forbid();

        var mechanic = await _context.Mechanics
            .FirstOrDefaultAsync(x => x.UserId == userId.Value);

        if (mechanic == null)
            return null;

        return await _context.ServiceRequests
            .FirstOrDefaultAsync(x =>
                x.Id == requestId &&
                x.MechanicId == mechanic.Id);
    }
}