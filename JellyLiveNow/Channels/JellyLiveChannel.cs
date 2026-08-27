using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JellyLiveNow.Services;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace JellyLiveNow.Channels;

/// <summary>
/// Native Jellyfin channel that exposes the currently active Live TV channel.
/// </summary>
public class JellyLiveChannel : IChannel, IHasFolderAttributes, IRequiresMediaInfoCallback
{
    private readonly LiveNowManager _liveNowManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly ILogger<JellyLiveChannel> _logger;

    public JellyLiveChannel(
        LiveNowManager liveNowManager,
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        ILogger<JellyLiveChannel> logger)
    {
        _liveNowManager = liveNowManager;
        _libraryManager = libraryManager;
        _mediaSourceManager = mediaSourceManager;
        _logger = logger;
    }

    public string Name => Plugin.Instance?.Configuration.ChannelName ?? "Viendo en TV";
    public string Description => "Canal en directo activo actualmente en el servidor";
    public string DataVersion => "1.0.1.0";
    public string HomePageUrl => string.Empty;
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;
    public string[] Attributes => new[] { "ViendoEnTV" };

    public InternalChannelFeatures GetChannelFeatures() => new()
    {
        ContentTypes = new List<ChannelMediaContentType> { ChannelMediaContentType.TvExtra },
        MediaTypes = new List<ChannelMediaType> { ChannelMediaType.Video },
        MaxPageSize = 1,
        SupportsContentDownloading = false
    };

    public bool IsEnabledFor(string userId)
    {
        if (Plugin.Instance?.Configuration.EnableJellyLiveNow == false)
        {
            return false;
        }

        _liveNowManager.RefreshActiveChannelState();
        return _liveNowManager.ActiveChannelId != Guid.Empty;
    }

    public Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        _liveNowManager.RefreshActiveChannelState();
        var result = new ChannelItemResult { Items = Array.Empty<ChannelItemInfo>() };
        var activeChannelId = _liveNowManager.ActiveChannelId;
        if (activeChannelId == Guid.Empty)
        {
            return Task.FromResult(result);
        }

        var channelItem = _libraryManager.GetItemById(activeChannelId) as LiveTvChannel;
        var name = _liveNowManager.ActiveChannelName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = channelItem?.Name ?? "Canal Live TV";
        }

        var programTitle = _liveNowManager.ActiveProgramTitle;
        var programOverview = _liveNowManager.ActiveOverview;
        var overview = !string.IsNullOrEmpty(programTitle)
            ? (!string.IsNullOrEmpty(programOverview) ? $"{programTitle}\n{programOverview}" : programTitle)
            : channelItem?.Overview ?? string.Empty;

        result.Items = new List<ChannelItemInfo>
        {
            new()
            {
                Id = activeChannelId.ToString("N"),
                Name = name,
                Type = ChannelItemType.Media,
                MediaType = ChannelMediaType.Video,
                IsLiveStream = true,
                Overview = overview,
                ImageUrl = _liveNowManager.ActiveImageUrl
            }
        };
        result.TotalRecordCount = 1;
        return Task.FromResult(result);
    }

    public async Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var channelGuid))
        {
            _logger.LogWarning("Invalid Live TV channel id {ChannelId}", id);
            return Array.Empty<MediaSourceInfo>();
        }

        if (_libraryManager.GetItemById(channelGuid) is not LiveTvChannel liveChannel)
        {
            _logger.LogWarning("Live TV channel {ChannelId} no longer exists", channelGuid);
            return Array.Empty<MediaSourceInfo>();
        }

        // Live TV sources are dynamic. GetPlaybackMediaSources asks Jellyfin's registered
        // IMediaSourceProviders (including LiveTvMediaSourceProvider) for the proper source,
        // OpenToken and live-stream metadata. GetStaticMediaSources alone can miss these.
        return await _mediaSourceManager
            .GetPlaybackMediaSources(liveChannel, null!, false, false, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
        => Task.FromResult<DynamicImageResponse>(null!);

    public IEnumerable<ImageType> GetSupportedChannelImages()
        => Array.Empty<ImageType>();
}
