using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Identity.Learners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Identity.Languages;

[ApiController]
[Authorize]
[Route("api/v1/learners/me")]
public sealed class LearnerLanguagesController(ILanguageDirectory directory) : ControllerBase
{
    // Мови цього learner і активна мова; дані лише власника (AC-10) — learner визначається з initData, не з URL.
    [HttpGet("languages", Name = "listLearnerLanguages")]
    [ProducesResponseType<LearnerLanguagesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListLearnerLanguages(CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId)
        {
            return NotFound(IdentityError.NotOnboarded());
        }

        var response = await directory.ListLearnerLanguagesAsync(learnerId, cancellationToken);
        return response is null ? NotFound(IdentityError.NotOnboarded()) : Ok(response);
    }
}
