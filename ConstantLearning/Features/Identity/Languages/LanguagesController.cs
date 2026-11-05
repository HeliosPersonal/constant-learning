using ConstantLearning.Features.Identity.Learners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Identity.Languages;

[ApiController]
[Authorize]
[Route("api/v1/languages")]
public sealed class LanguagesController(ILanguageDirectory directory) : ControllerBase
{
    // Каталог мов доступний до завершення онбордингу — learner бачить вибір мов одразу (US-01).
    [HttpGet(Name = "listTargetLanguages")]
    [ProducesResponseType<TargetLanguageCatalogResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListTargetLanguages(CancellationToken cancellationToken) =>
        Ok(await directory.ListCatalogAsync(cancellationToken));
}
