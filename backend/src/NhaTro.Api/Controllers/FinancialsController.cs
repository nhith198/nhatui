using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NhaTro.Api.Contracts;
using NhaTro.Application.Dtos;
using NhaTro.Application.UseCases;
using NhaTro.Application.Abstractions;

namespace NhaTro.Api.Controllers;

[PagePermission(EmployeePages.Financials), Route("api/financials")]
public class FinancialsController(FinancialService service) : ApiBase
{
    [HttpGet]
    public Task<FinancialReportDto> Report(
        [FromQuery, Required, RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$")] string month,
        Guid? propertyId, CancellationToken ct) => service.Report(UserId, month, propertyId, ct);

    [HttpPost("expenses")]
    public async Task<IActionResult> CreateExpense(FinancialExpenseRequest request, CancellationToken ct)
    {
        var id = await service.CreateExpense(UserId, Input(request), ct);
        return Created($"/api/financials/expenses/{id}", new { id });
    }

    [HttpPut("expenses/{id:guid}")]
    public async Task<IActionResult> UpdateExpense(Guid id, FinancialExpenseRequest request, CancellationToken ct)
    {
        await service.UpdateExpense(UserId, id, Input(request), ct);
        return NoContent();
    }

    [HttpDelete("expenses/{id:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid id, CancellationToken ct)
    {
        await service.DeleteExpense(UserId, id, ct);
        return NoContent();
    }

    [HttpDelete("deposits/{id:guid}")]
    public async Task<IActionResult> DeleteDepositReceipt(Guid id, CancellationToken ct)
    {
        await service.DeleteDepositReceipt(UserId, id, ct);
        return NoContent();
    }

    private static FinancialExpenseInput Input(FinancialExpenseRequest request) =>
        new(request.PropertyId, request.Category, request.Description, request.Recipient, request.Amount, request.Date, request.IsMonthly, request.IsPaid);
}