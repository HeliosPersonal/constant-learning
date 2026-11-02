using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Learning.Sessions.Start;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Learning.Disputes;

[ApiController]
[Authorize]
[Route("api/v1/answers/{answerId:guid}/dispute")]
public sealed class DisputeController(DisputeService disputes) : ControllerBase
{
    [HttpPost(Name = "disputeAnswer")]
    [ProducesResponseType<DisputeResult>(200)]
    [ProducesResponseType<SessionError>(401)]
    [ProducesResponseType<SessionError>(404)]
    [ProducesResponseType<SessionError>(409)]
    public async Task<IActionResult> Post(Guid answerId, CancellationToken ct)
    {
        if (User.GetLearnerId() is not { } learnerId) return NotFound(new SessionError("answer.not_found", "Відповідь не знайдено."));
        var result = await disputes.DisputeAsync(learnerId, answerId, ct);
        return StatusCode(result.Status, (object?)result.Result ?? result.Error);
    }
}
