using System.Text.Json.Serialization;
using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Identity.Learners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Identity.Access;

/// <summary>Повідомляє Mini App про ролі користувача, щоб показати вхід до редактора лише редакторам.</summary>
[ApiController]
[Authorize]
[Route("api/v1/learners/me/access")]
public sealed class LearnerAccessController : ControllerBase
{
    [HttpGet(Name = "getLearnerAccess")]
    [ProducesResponseType<LearnerAccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status401Unauthorized)]
    public IActionResult GetAccess() =>
        Ok(new LearnerAccessResponse(User.IsInRole(TelegramAuthentication.ContentEditorRole)));
}

public sealed record LearnerAccessResponse([property: JsonPropertyName("content_editor")] bool ContentEditor);
