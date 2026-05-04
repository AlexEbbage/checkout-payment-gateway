using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using PaymentGateway.Api.Application.Payments;
using PaymentGateway.Api.Contracts.Payments;

namespace PaymentGateway.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private const int Status425TooEarly = 425;

    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProcessPaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), Status425TooEarly)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ProcessPayment(
        [FromBody] ProcessPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.ProcessPaymentAsync(
            request,
            idempotencyKey,
            cancellationToken);

        return result.Outcome switch
        {
            PaymentProcessingOutcome.Succeeded => CreatedAtAction(
                nameof(GetPayment),
                new { id = result.Response!.Id },
                result.Response),

            PaymentProcessingOutcome.Rejected => ValidationProblem(result),

            PaymentProcessingOutcome.IdempotencyConflict => Problem(
                title: "Idempotency conflict",
                detail: result.ErrorMessage,
                statusCode: StatusCodes.Status409Conflict),

            PaymentProcessingOutcome.IdempotencyInProgress => Problem(
                title: "Idempotent request already in progress",
                detail: result.ErrorMessage,
                statusCode: Status425TooEarly),

            PaymentProcessingOutcome.BankUnavailable => Problem(
                title: "Payment processing temporarily unavailable",
                detail: result.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable),

            _ => Problem(
                title: "Unexpected payment processing result",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayment(
        Guid id,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentService.GetPaymentAsync(id, cancellationToken);

        if (payment is null)
        {
            return NotFound();
        }

        return Ok(payment);
    }

    private IActionResult ValidationProblem(PaymentProcessingResult result)
    {
        var modelState = new ModelStateDictionary();

        foreach (var error in result.ValidationErrors!)
        {
            modelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(modelState);
    }
}
