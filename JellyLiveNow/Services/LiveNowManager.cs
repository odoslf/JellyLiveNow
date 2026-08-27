using System;
using System.Collections.Concurrent;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace JellyLiveNow.Services;

/// <summary>
/// Tracks the currently active Live TV channel by evaluating Jellyfin sessions on demand.
/// Keeping this service passive avoids participating in Jellyfin's hosted-service startup graph.
/// </summary>
public sealed class LiveNowManager
{
    private readonly ISessionManager _sessionManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<LiveNowManager> _logger;
    private readonly ConcurrentDictionary<Guid, byte> _dismissedUserIds = new();
    private readonly object _lock = new();

    private Guid _activeChannelId = Guid.Empty;
    private string _activeChannelName = string.Empty;
    private string _activeProgramTitle = string.Empty;
    private string _activeOverview = string.Empty;
    private string _activeImageUrl = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveNowManager"/> class.
    /// </summary>
    public LiveNowManager(
        ISessionManager sessionManager,
        ILibraryManager libraryManager,
        ILogger<LiveNowManager> logger)
    {
        _sessionManager = sessionManager;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>Gets the active Live TV channel GUID.</summary>
    public Guid ActiveChannelId { get { lock (_lock) { return _activeChannelId; } } }

    /// <summary>Gets the active Live TV channel name.</summary>
    public string ActiveChannelName { get { lock (_lock) { return _activeChannelName; } } }

    /// <summary>Gets the current program title if available.</summary>
    public string ActiveProgramTitle { get { lock (_lock) { return _activeProgramTitle; } } }

    /// <summary>Gets the active channel overview.</summary>
    public string ActiveOverview { get { lock (_lock) { return _activeOverview; } } }

    /// <summary>Gets a Jellyfin API image URL for the active channel if available.</summary>
    public string ActiveImageUrl { get { lock (_lock) { return _activeImageUrl; } } }

    /// <summary>Evaluates current active sessions and updates the active Live TV channel state.</summary>
    public void RefreshActiveChannelState()
    {
        if (Plugin.Instance?.Configuration.EnableJellyLiveNow == false)
        {
            ClearActiveState();
            return;
        }

        try
        {
            var activeSessions = _sessionManager.Sessions;
            Guid foundChannelId = Guid.Empty;
            BaseItem? foundItem = null;

            foreach (var session in activeSessions)
            {
                BaseItem? item = session.FullNowPlayingItem;
                if (item == null && session.NowPlayingItem != null && session.NowPlayingItem.Id != Guid.Empty)
                {
                    item = _libraryManager.GetItemById(session.NowPlayingItem.Id);
                }

                if (item != null && TryGetLiveTvChannelId(item, out var channelId))
                {
                    foundChannelId = channelId;
                    foundItem = item;
                    break;
                }
            }

            if (foundChannelId != Guid.Empty)
            {
                UpdateActiveChannel(foundChannelId, foundItem);
            }
            else
            {
                ClearActiveState();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing active Live TV channel state");
        }
    }

    /// <summary>Attempts to extract the real Live TV channel GUID from a BaseItem.</summary>
    public static bool TryGetLiveTvChannelId(BaseItem item, out Guid channelId)
    {
        channelId = Guid.Empty;
        if (item is LiveTvChannel liveChannel)
        {
            channelId = liveChannel.Id;
            return true;
        }

        if (item is LiveTvProgram liveProgram && liveProgram.ChannelId != Guid.Empty)
        {
            channelId = liveProgram.ChannelId;
            return true;
        }

        var itemNamespace = item.GetType().Namespace;
        if (item.ChannelId != Guid.Empty
            && itemNamespace != null
            && itemNamespace.Contains("LiveTv", StringComparison.Ordinal))
        {
            channelId = item.ChannelId;
            return true;
        }

        return false;
    }

    private void UpdateActiveChannel(Guid channelId, BaseItem? item)
    {
        lock (_lock)
        {
            if (_activeChannelId != channelId)
            {
                _dismissedUserIds.Clear();
                _activeChannelId = channelId;
            }

            var channelItem = _libraryManager.GetItemById(channelId) as LiveTvChannel ?? item as LiveTvChannel;
            _activeChannelName = channelItem?.Name ?? item?.Name ?? "Live TV";

            if (item is LiveTvProgram program)
            {
                _activeProgramTitle = program.Name;
                _activeOverview = program.Overview ?? string.Empty;
            }
            else
            {
                _activeProgramTitle = string.Empty;
                _activeOverview = channelItem?.Overview ?? string.Empty;
            }

            _activeImageUrl = channelItem?.HasImage(ImageType.Primary, 0) == true
                ? $"/Items/{channelId:N}/Images/Primary"
                : string.Empty;
        }
    }

    private void ClearActiveState()
    {
        lock (_lock)
        {
            _activeChannelId = Guid.Empty;
            _activeChannelName = string.Empty;
            _activeProgramTitle = string.Empty;
            _activeOverview = string.Empty;
            _activeImageUrl = string.Empty;
            _dismissedUserIds.Clear();
        }
    }

    /// <summary>Checks if the current banner notification is dismissed for the specified user.</summary>
    public bool IsDismissedForUser(Guid userId) => _dismissedUserIds.ContainsKey(userId);

    /// <summary>Dismisses the current active channel notification for the specified user.</summary>
    public void DismissForUser(Guid userId)
    {
        if (userId != Guid.Empty)
        {
            _dismissedUserIds.TryAdd(userId, 0);
        }
    }
}
