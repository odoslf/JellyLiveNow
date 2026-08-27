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
        // LiveNowManager is intentionally passive and evaluates sessions on demand.
        // This keeps the plugin out of Jellyfin's hosted-service startup graph while
        // preserving the same native-channel and API behaviour.
        serviceCollection.AddSingleton<LiveNowManager>();
        serviceCollection.AddSingleton<IChannel, JellyLiveChannel>();
    }
}
