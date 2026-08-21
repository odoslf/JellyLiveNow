namespace JellyLiveNow.Api.Models;

/// <summary>
/// API response model for active channel status.
/// </summary>
public class ActiveChannelResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether a Live TV channel is active and visible.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the active Live TV channel GUID string.
    /// </summary>
    public string? ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the channel name.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    /// Gets or sets the current program title if available.
    /// </summary>
    public string? ProgramTitle { get; set; }

    /// <summary>
    /// Gets or sets the program or channel overview description.
    /// </summary>
    public string? Overview { get; set; }

    /// <summary>
    /// Gets or sets the channel image URL.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current requesting user dismissed this active channel notification.
    /// </summary>
    public bool IsDismissed { get; set; }
}
