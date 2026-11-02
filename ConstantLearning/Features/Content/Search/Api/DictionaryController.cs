using ConstantLearning.Features.Identity.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Content.Search.Api;

[ApiController]
[Authorize]
[Route("api/v1/dictionary")]
public sealed class DictionaryController(DictionarySearchService search) : ControllerBase
{
    [HttpGet("search", Name = "searchDictionary")]
    public async Task<IActionResult> Search(CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId) return NotAuthenticated();
        if (DictionarySearchRequest.Read(Request.Query, out var request) is { } error) return BadRequest(error);
        var page = await search.SearchAsync(learnerId, request!, cancellationToken);
        return page is null ? BadRequest(DictionarySearchRequest.Invalid()) : Ok(page);
    }

    [HttpGet("entries/{entry_id}", Name = "getDictionaryEntry")]
    public async Task<IActionResult> GetEntry(string entry_id, CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId) return NotAuthenticated();
        if (!Guid.TryParseExact(entry_id, "D", out var entryId)) return MissingEntry();
        var card = await search.GetEntryAsync(learnerId, entryId, cancellationToken);
        return card is null ? MissingEntry() : Ok(card);
    }

    private UnauthorizedObjectResult NotAuthenticated() =>
        Unauthorized(new DictionaryError("auth.invalid_init_data", "Відкрийте тренер через Telegram."));

    private NotFoundObjectResult MissingEntry() =>
        NotFound(new DictionaryError("dictionary.entry_not_found", "Слово не знайдено."));
}
