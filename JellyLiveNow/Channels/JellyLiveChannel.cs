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
/// Native Jellyfin IChannel implementation for Android TV and all Jellyfin clients.
/// </summary>
public class JellyLiveChannel : IChannel, IHasFolderAttributes, IRequiresMediaInfoCallback
{
    private readonly LiveNowManager _liveNowManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly ILogger<JellyLiveChannel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyLiveChannel"/> class.
    /// </summary>
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

    /// <inheritdoc />
    public string Name => Plugin.Instance?.Configuration.ChannelName ?? "Viendo en TV";

    /// <inheritdoc />
    public string Description => "Canal en directo activo actualmente en el servidor";

    /// <inheritdoc />
    public string DataVersion => "1.0.0.0";

    /// <inheritdoc />
    public string HomePageUrl => string.Empty;

    /// <inheritdoc />
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    /// <inheritdoc />
    public string[] Attributes => new[] { "ViendoEnTV" };

    /// <inheritdoc />
    public InternalChannelFeatures GetChannelFeatures()
    {
        return new InternalChannelFeatures
        {
            ContentTypes = new List<ChannelMediaContentType> { ChannelMediaContentType.TvExtra },
            MediaTypes = new List<ChannelMediaType> { ChannelMediaType.Video },
            MaxPageSize = 1,
            SupportsContentDownloading = false
        };
    }

    /// <inheritdoc />
    public bool IsEnabledFor(string userId)
    {
        if (Plugin.Instance?.Configuration.EnableJellyLiveNow == false)
        {
            return false;
        }

        // Only enabled/visible when there is an active Live TV channel playback session
        return _liveNowManager.ActiveChannelId != Guid.Empty;
    }

    /// <inheritdoc />
    public Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        var result = new ChannelItemResult
        {
            Items = Array.Empty<ChannelItemInfo>()
        };

        var activeChannelId = _liveNowManager.ActiveChannelId;
        if (activeChannelId == Guid.Empty)
        {
            return Task.FromResult(result);
        }

        var channelItem = _libraryManager.GetItemById(activeChannelId) as LiveTvChannel;
        var name = _liveNowManager.ActiveChannelName;
        if (string.IsNullOrEmpty(name) && channelItem != null)
        {
            name = channelItem.Name;
        }

        if (string.IsNullOrEmpty(name))
        {
            name = "Canal Live TV";
        }

        var overview = string.Empty;
        var progTitle = _liveNowManager.ActiveProgramTitle;
        var progOverview = _liveNowManager.ActiveOverview;

        if (!string.IsNullOrEmpty(progTitle))
        {
            overview = !string.IsNullOrEmpty(progOverview) ? $"{progTitle}\n{progOverview}" : progTitle;
        }
        else if (channelItem != null)
        {
            overview = channelItem.Overview ?? string.Empty;
        }

        var itemInfo = new ChannelItemInfo
        {
            Id = activeChannelId.ToString("N"),
            Name = name,
            Type = ChannelItemType.Media,
            MediaType = ChannelMediaType.Video,
            IsLiveStream = true,
            Overview = overview,
            ImageUrl = _liveNowManager.ActiveImageUrl
        };

        result.Items = new List<ChannelItemInfo> { itemInfo };
        result.TotalRecordCount = 1;

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string id, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(id, out var channelGuid))
        {
            var item = _libraryManager.GetItemById(channelGuid);
            if (item is LiveTvChannel liveChannel)
            {
                var staticSources = _mediaSourceManager.GetStaticMediaSources(liveChannel, false, null);
                return Task.FromResult<IEnumerable<MediaSourceInfo>>(staticSources);
            }
        }

        return Task.FromResult<IEnumerable<MediaSourceInfo>>(Array.Empty<MediaSourceInfo>());
    }

    /// <inheritdoc />
    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
    {
        return Task.FromResult<DynamicImageResponse>(null!);
    }

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedChannelImages()
    {
        return new List<ImageType> { ImageType.Primary };
    }
}
