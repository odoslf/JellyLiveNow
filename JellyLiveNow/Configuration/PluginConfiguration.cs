using MediaBrowser.Model.Plugins;

namespace JellyLiveNow.Configuration;

/// <summary>
/// Plugin configuration settings for JellyLiveNow.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether JellyLiveNow is enabled.
    /// </summary>
    public bool EnableJellyLiveNow { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Web banner is enabled.
    /// </summary>
    public bool EnableWebBanner { get; set; } = true;

    /// <summary>
    /// Gets or sets the channel name displayed in Jellyfin interfaces.
    /// </summary>
    public string ChannelName { get; set; } = "Viendo en TV";
}
