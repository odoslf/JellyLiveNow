using System;
using System.Security.Claims;
using JellyLiveNow.Api.Models;
using JellyLiveNow.Configuration;
using JellyLiveNow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JellyLiveNow.Api;

/// <summary>
/// Web API controller for JellyLiveNow status, banner dismissals, and plugin configuration.
/// </summary>
[ApiController]
[Route("JellyLiveNow")]
public class JellyLiveNowController : ControllerBase
{
    private readonly LiveNowManager _liveNowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyLiveNowController"/> class.
    /// </summary>
    public JellyLiveNowController(LiveNowManager liveNowManager)
    {
        _liveNowManager = liveNowManager;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
                          ?? User.FindFirst("UserId")?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Gets the current active Live TV channel status.
    /// </summary>
    [HttpGet("Status")]
    [HttpGet("ActiveChannel")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ActiveChannelResponse> GetActiveChannel()
    {
        _liveNowManager.RefreshActiveChannelState();

        var activeId = _liveNowManager.ActiveChannelId;
        if (activeId == Guid.Empty || Plugin.Instance?.Configuration.EnableJellyLiveNow == false)
        {
            return Ok(new ActiveChannelResponse { IsActive = false });
        }

        var userId = GetUserId();
        var isDismissed = _liveNowManager.IsDismissedForUser(userId);

        return Ok(new ActiveChannelResponse
        {
            IsActive = !isDismissed,
            ChannelId = activeId.ToString("N"),
            ChannelName = _liveNowManager.ActiveChannelName,
            ProgramTitle = _liveNowManager.ActiveProgramTitle,
            Overview = _liveNowManager.ActiveOverview,
            ImageUrl = _liveNowManager.ActiveImageUrl,
            IsDismissed = isDismissed
        });
    }

    /// <summary>
    /// Dismisses the active channel notification for the current requesting user.
    /// </summary>
    [HttpPost("Dismiss")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult DismissNotification()
    {
        var userId = GetUserId();
        if (userId != Guid.Empty)
        {
            _liveNowManager.DismissForUser(userId);
        }

        return Ok();
    }

    /// <summary>
    /// Gets the plugin configuration.
    /// </summary>
    [HttpGet("Configuration")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<PluginConfiguration> GetConfiguration()
    {
        return Ok(Plugin.Instance?.Configuration ?? new PluginConfiguration());
    }

    /// <summary>
    /// Updates the plugin configuration.
    /// </summary>
    [HttpPost("Configuration")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult UpdateConfiguration([FromBody] PluginConfiguration config)
    {
        Plugin.Instance?.UpdateConfiguration(config);
        _liveNowManager.RefreshActiveChannelState();
        return NoContent();
    }
}
