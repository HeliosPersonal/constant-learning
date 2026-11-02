using System.Text.Json;
using ConstantLearning.Features.Identity.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Content.Reports;

[ApiController]
[Authorize]
[Route("api/v1/content-reports")]
public sealed class ContentReportsController(ContentReportService reports) : ControllerBase
{
    [HttpPost(Name = "createContentReport")]
    [ProducesResponseType<ContentReportAccepted>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ContentReportError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ContentReportError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ContentReportError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ContentReportError>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateContentReport(CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId)
        {
            return NotFound(new ContentReportError("learner.not_onboarded", "Спершу налаштуйте тренер."));
        }

        ContentReportRequest? request;
        try
        {
            using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
            if (ContentReportRequest.Read(document.RootElement, out request) is { } error)
            {
                return BadRequest(error);
            }
        }
        catch (JsonException)
        {
            return BadRequest(ContentReportRequest.Invalid());
        }

        var result = await reports.CreateAsync(learnerId, request!, cancellationToken);
        return StatusCode(result.StatusCode, result.Report is { } report ? report : result.Error);
    }
}
