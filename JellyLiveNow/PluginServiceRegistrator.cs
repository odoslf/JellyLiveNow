using JellyLiveNow.Services;
using MediaBrowser.Controller;
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
        serviceCollection.AddHostedService<LiveNowManager>(sp => sp.GetRequiredService<LiveNowManager>());
    }
}
