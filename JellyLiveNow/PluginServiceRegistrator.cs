using JellyLiveNow.Channels;
using JellyLiveNow.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace JellyLiveNow;

/// <summary>
/// Registers services for JellyLiveNow into Jellyfin dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<LiveNowManager>();
        serviceCollection.AddHostedService(sp => sp.GetRequiredService<LiveNowManager>());

        // ChannelManager receives IEnumerable<IChannel> from DI. Registering the
        // implementation as IChannel is therefore required for Jellyfin to discover it.
        serviceCollection.AddSingleton<IChannel, JellyLiveChannel>();
    }
}
