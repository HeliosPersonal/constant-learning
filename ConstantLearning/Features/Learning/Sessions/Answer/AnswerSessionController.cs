using System.Text.Json;
using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Learning.Sessions.Start;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Learning.Sessions.Answer;

[ApiController]
[Authorize]
[Route("api/v1/sessions/{sessionId:guid}/items/{itemId:guid}/answer")]
public sealed class AnswerSessionController(AnswerSessionService answers) : ControllerBase
{
    [HttpPost(Name = "answerSessionItem")]
    [ProducesResponseType<AnswerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SessionError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<SessionError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<SessionError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<SessionError>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Answer(Guid sessionId, Guid itemId, CancellationToken cancellationToken)
    {
        JsonDocument document;
        try { document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken); }
        catch (JsonException) { return BadRequest(AnswerRequest.Invalid()); }
        using (document)
        {
            var invalid = AnswerRequest.Read(document.RootElement, out var request);
            if (invalid is not null) return BadRequest(invalid);
            if (User.GetLearnerId() is not { } learnerId)
                return NotFound(new SessionError("session.item_not_found", "Картку не знайдено."));
            var result = await answers.AnswerAsync(learnerId, sessionId, itemId, request!, cancellationToken);
            return StatusCode(result.StatusCode, (object?)result.Answer ?? result.Error);
        }
    }
}
