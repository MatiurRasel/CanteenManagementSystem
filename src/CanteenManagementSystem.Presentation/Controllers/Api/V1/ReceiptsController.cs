using Asp.Versioning;
using Platform.Application.Abstractions.Receipts;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers.Api.V1;

/// <summary>Receipt + kitchen ticket PDF generation.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/receipts")]
public sealed class ReceiptsController : ControllerBase
{
    private readonly IReceiptService _receipts;

    public ReceiptsController(IReceiptService receipts) => _receipts = receipts;

    /// <summary>Customer-facing A5 PDF with VAT line + verification QR.</summary>
    [HttpGet("{orderId:int}/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Pdf(int orderId, CancellationToken cancellationToken)
    {
        var receipt = await _receipts.GenerateOrderReceiptAsync(orderId, cancellationToken);
        return File(receipt.Bytes, receipt.ContentType, receipt.FileName);
    }

    /// <summary>80mm thermal kitchen ticket (no VAT, big font).</summary>
    [HttpGet("{orderId:int}/kitchen")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Kitchen(int orderId, CancellationToken cancellationToken)
    {
        var ticket = await _receipts.GenerateKitchenTicketAsync(orderId, cancellationToken);
        return File(ticket.Bytes, ticket.ContentType, ticket.FileName);
    }
}
