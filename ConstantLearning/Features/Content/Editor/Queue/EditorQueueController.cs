using System.Text.Json;
using ConstantLearning.Features.Identity.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Content.Editor.Queue;

[ApiController]
[Authorize(Policy = TelegramAuthentication.ContentEditorPolicy)]
[Route("api/v1/editor/content-reports")]
public sealed class EditorQueueController(EditorQueueService queue) : ControllerBase
{
    [HttpGet(Name = "listEditorQueueByLanguage")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (EditorQueueRequest.Read(Request.Query, out var request) is { } error) return BadRequest(error);
        var page = await queue.ListAsync(request!, cancellationToken);
        return page is null ? BadRequest(EditorQueueRequest.Invalid()) : Ok(page);
    }

    [HttpPost("{report_id}/decision", Name = "decideContentReport")]
    public async Task<IActionResult> Decide(string report_id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(report_id, "D", out var reportId)) return NotFound(EditorQueueService.NotFound());
        EditorDecision? decision;
        try
        {
            using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
            if (EditorDecision.Read(document.RootElement, out decision) is { } error) return BadRequest(error);
        }
        catch (JsonException) { return BadRequest(EditorQueueRequest.Invalid()); }
        var result = await queue.DecideAsync(reportId, User.GetLearnerId(), decision!, cancellationToken);
        return StatusCode(result.Status, result.Report is { } closed ? closed : result.Error);
    }
}
