using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using JellyLiveNow.Api;
using JellyLiveNow.Api.Models;
using JellyLiveNow.Channels;
using JellyLiveNow.Configuration;
using JellyLiveNow.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JellyLiveNow.Tests;

public class LiveNowTests
{
    private readonly Mock<ISessionManager> _sessionManagerMock;
    private readonly Mock<ILibraryManager> _libraryManagerMock;
    private readonly Mock<ILiveTvManager> _liveTvManagerMock;
    private readonly Mock<IMediaSourceManager> _mediaSourceManagerMock;
    private readonly Mock<ILogger<LiveNowManager>> _managerLoggerMock;
    private readonly Mock<ILogger<JellyLiveChannel>> _channelLoggerMock;
    private readonly Mock<ILogger> _sessionLoggerMock;

    public LiveNowTests()
    {
        _sessionManagerMock = new Mock<ISessionManager>();
        _libraryManagerMock = new Mock<ILibraryManager>();
        _liveTvManagerMock = new Mock<ILiveTvManager>();
        _mediaSourceManagerMock = new Mock<IMediaSourceManager>();
        _managerLoggerMock = new Mock<ILogger<LiveNowManager>>();
        _channelLoggerMock = new Mock<ILogger<JellyLiveChannel>>();
        _sessionLoggerMock = new Mock<ILogger>();

        // Initialize Plugin instance
        var appPathsMock = new Mock<IApplicationPaths>();
        appPathsMock.Setup(a => a.PluginsPath).Returns("/tmp/plugins");
        appPathsMock.Setup(a => a.PluginConfigurationsPath).Returns("/tmp/pluginconfigs");
        var xmlSerializerMock = new Mock<IXmlSerializer>();

        _ = new Plugin(appPathsMock.Object, xmlSerializerMock.Object);
        Plugin.Instance!.UpdateConfiguration(new PluginConfiguration());
    }

    private LiveNowManager CreateManager()
    {
        return new LiveNowManager(
            _sessionManagerMock.Object,
            _libraryManagerMock.Object,
            _liveTvManagerMock.Object,
            _managerLoggerMock.Object);
    }

    private JellyLiveChannel CreateChannel(LiveNowManager manager)
    {
        return new JellyLiveChannel(
            manager,
            _libraryManagerMock.Object,
            _mediaSourceManagerMock.Object,
            _channelLoggerMock.Object);
    }

    private SessionInfo CreateSession(string id, BaseItem item, Guid userId = default)
    {
        var session = new SessionInfo(_sessionManagerMock.Object, _sessionLoggerMock.Object)
        {
            Id = id,
            FullNowPlayingItem = item,
            UserId = userId == default ? Guid.NewGuid() : userId
        };
        return session;
    }

    private JellyLiveNowController CreateController(LiveNowManager manager, Guid userId)
    {
        var controller = new JellyLiveNowController(manager);
        var httpContext = new DefaultHttpContext();
        if (userId != Guid.Empty)
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        return controller;
    }

    [Fact]
    public async Task Case1_NoLiveTvSession_ChannelDisabledAndStatusInactive()
    {
        _sessionManagerMock.Setup(s => s.Sessions).Returns(new List<SessionInfo>());
        var manager = CreateManager();
        manager.RefreshActiveChannelState();

        var channel = CreateChannel(manager);
        Assert.False(channel.IsEnabledFor("user1"));

        var items = await channel.GetChannelItems(new InternalChannelItemQuery(), CancellationToken.None);
        Assert.Empty(items.Items);

        var controller = CreateController(manager, Guid.NewGuid());
        var statusResult = controller.GetActiveChannel();
        var okObject = Assert.IsType<OkObjectResult>(statusResult.Result);
        var response = Assert.IsType<ActiveChannelResponse>(okObject.Value);
        Assert.False(response.IsActive);
    }

    [Fact]
    public void Case2_MoviePlayback_DoesNotActivateJellyLiveNow()
    {
        var movie = new Movie { Id = Guid.NewGuid(), Name = "Inception" };
        var session = CreateSession("session1", movie);
        _sessionManagerMock.Setup(s => s.Sessions).Returns(new[] { session });

        var manager = CreateManager();
        manager.RefreshActiveChannelState();

        Assert.Equal(Guid.Empty, manager.ActiveChannelId);
        var channel = CreateChannel(manager);
        Assert.False(channel.IsEnabledFor("user1"));
    }

    [Fact]
    public void Case3_EpisodePlayback_DoesNotActivateJellyLiveNow()
    {
        var episode = new Episode { Id = Guid.NewGuid(), Name = "Pilot Episode" };
        var session = CreateSession("session1", episode);
        _sessionManagerMock.Setup(s => s.Sessions).Returns(new[] { session });

        var manager = CreateManager();
        manager.RefreshActiveChannelState();

        Assert.Equal(Guid.Empty, manager.ActiveChannelId);
        var channel = CreateChannel(manager);
        Assert.False(channel.IsEnabledFor("user1"));
    }

    [Fact]
    public async Task Case4_LiveTvPlayback_DetectsCorrectLiveTvChannel()
    {
        var liveChannelId = Guid.NewGuid();
        var liveChannel = new LiveTvChannel { Id = liveChannelId, Name = "Movistar Fútbol" };
        var session = CreateSession("session1", liveChannel);

        _sessionManagerMock.Setup(s => s.Sessions).Returns(new[] { session });
        _libraryManagerMock.Setup(l => l.GetItemById(liveChannelId)).Returns(liveChannel);

        var manager = CreateManager();
        manager.RefreshActiveChannelState();

        Assert.Equal(liveChannelId, manager.ActiveChannelId);
        Assert.Equal("Movistar Fútbol", manager.ActiveChannelName);

        var channel = CreateChannel(manager);
        Assert.True(channel.IsEnabledFor("user1"));

        var itemsResult = await channel.GetChannelItems(new InternalChannelItemQuery(), CancellationToken.None);
        Assert.Single(itemsResult.Items);
        Assert.Equal("Movistar Fútbol", itemsResult.Items[0].Name);
    }

    [Fact]
    public void Case5_SecondUserQueries_ReceivesSameActiveChannel()
    {
        var liveChannelId = Guid.NewGuid();
        var liveChannel = new LiveTvChannel { Id = liveChannelId, Name = "Movistar Fútbol" };
        var sessionUser1 = CreateSession("session1", liveChannel);

        _sessionManagerMock.Setup(s => s.Sessions).Returns(new[] { sessionUser1 });
        _libraryManagerMock.Setup(l => l.GetItemById(liveChannelId)).Returns(liveChannel);

        var manager = CreateManager();
        manager.RefreshActiveChannelState();

        var user2Id = Guid.NewGuid();
        var controllerUser2 = CreateController(manager, user2Id);
        var statusResult = controllerUser2.GetActiveChannel();
        var okObject = Assert.IsType<OkObjectResult>(statusResult.Result);
        var response = Assert.IsType<ActiveChannelResponse>(okObject.Value);

        Assert.True(response.IsActive);
        Assert.Equal(liveChannelId.ToString("N"), response.ChannelId);
        Assert.Equal("Movistar Fútbol", response.ChannelName);
    }

    [Fact]
    public async Task Case6_SecondUserPlaysChannel_ResolvesRealJellyfinChannelMediaSources()
    {
        var liveChannelId = Guid.NewGuid();
        var liveChannel = new LiveTvChannel { Id = liveChannelId, Name = "Movistar Fútbol" };
        var mockMediaSources = new List<MediaSourceInfo>
        {
            new MediaSourceInfo { Id = "source1", Path = "http://hdhomerun/stream1" }
        };

        _libraryManagerMock.Setup(l => l.GetItemById(liveChannelId)).Returns(liveChannel);
        _mediaSourceManagerMock.Setup(m => m.GetStaticMediaSources(liveChannel, false, null)).Returns(mockMediaSources);

        var manager = CreateManager();
        var channel = CreateChannel(manager);

        var sources = await channel.GetChannelItemMediaInfo(liveChannelId.ToString("N"), CancellationToken.None);
        var sourceList = Assert.IsAssignableFrom<IEnumerable<MediaSourceInfo>>(sources);
        Assert.Single(sourceList);
    }

    [Fact]
    public async Task Case7_TwoUsersWatchingSameChannel_SingleEntryDisplayed()
    {
        var liveChannelId = Guid.NewGuid();
        var liveChannel = new LiveTvChannel { Id = liveChannelId, Name = "Movistar Fútbol" };
        var session1 = CreateSession("s1", liveChannel, Guid.NewGuid());
        var session2 = CreateSession("s2", liveChannel, Guid.NewGuid());

        _sessionManagerMock.Setup(s => s.Sessions).Returns(new[] { session1, session2 });
        _libraryManagerMock.Setup(l => l.GetItemById(liveChannelId)).Returns(liveChannel);

        var manager = CreateManager();
        manager.RefreshActiveChannelState();

        var channel = CreateChannel(manager);
        var itemsResult = await channel.GetChannelItems(new InternalChannelItemQuery(), CancellationToken.None);

        Assert.Single(itemsResult.Items);
        Assert.Equal(liveChannelId.ToString("N"), itemsResult.Items[0].Id);
    }

    [Fact]
    public void Case8_OneUserLeavesButOtherContinues_ChannelRemainsActive()
    {
        var liveChannelId = Guid.NewGuid();
        var liveChannel = new LiveTvChannel { Id = liveChannelId, Name = "Movistar Fútbol" };
        var session1 = CreateSession("s1", liveChannel, Guid.NewGuid());
        var session2 = CreateSession("s2", liveChannel, Guid.NewGuid());

        // Two sessions active
        _sessionManagerMock.Setup(s => s.Sessions).Returns(new[] { session1, session2 });
        var manager = CreateManager();
        manager.RefreshActiveChannelState();
        Assert.Equal(liveChannelId, manager.ActiveChannelId);

        // Session 1 stops, Session 2 continues
        _sessionManagerMock.Setup(s => s.Sessions).Returns(new[] { session2 });
        manager.RefreshActiveChannelState();

        Assert.Equal(liveChannelId, manager.ActiveChannelId);
        var channel = CreateChannel(manager);
        Assert.True(channel.IsEnabledFor("user2"));
    }

    [Fact]
    public void Case9_AllUsersLeave_ChannelDisappears()
    {
        var liveChannelId = Guid.NewGuid();
        var liveChannel = new LiveTvChannel { Id = liveChannelId, Name = "Movistar Fútbol" };
        var session1 = CreateSession("s1", liveChannel, Guid.NewGuid());

        _sessionManagerMock.Setup(s => s.Sessions).Returns(new[] { session1 });
        var manager = CreateManager();
        manager.RefreshActiveChannelState();
        Assert.Equal(liveChannelId, manager.ActiveChannelId);

        // All sessions stop
        _sessionManagerMock.Setup(s => s.Sessions).Returns(new List<SessionInfo>());
        manager.RefreshActiveChannelState();

        Assert.Equal(Guid.Empty, manager.ActiveChannelId);
        var channel = CreateChannel(manager);
        Assert.False(channel.IsEnabledFor("user1"));
    }

    [Fact]
    public void Case10_UserDismissesWebBanner_DismissedForUserDuringEmission()
    {
        var liveChannelId = Guid.NewGuid();
        var liveChannel = new LiveTvChannel { Id = liveChannelId, Name = "Movistar Fútbol" };
        var session1 = CreateSession("s1", liveChannel, Guid.NewGuid());

        _sessionManagerMock.Setup(s => s.Sessions).Returns(new[] { session1 });
        _libraryManagerMock.Setup(l => l.GetItemById(liveChannelId)).Returns(liveChannel);

        var manager = CreateManager();
        manager.RefreshActiveChannelState();

        var user2Id = Guid.NewGuid();
        var controllerUser2 = CreateController(manager, user2Id);

        // Before dismiss
        var statusBefore = controllerUser2.GetActiveChannel();
        var respBefore = Assert.IsType<ActiveChannelResponse>(((OkObjectResult)statusBefore.Result!).Value);
        Assert.True(respBefore.IsActive);

        // Dismiss for user 2
        controllerUser2.DismissNotification();

        // After dismiss
        var statusAfter = controllerUser2.GetActiveChannel();
        var respAfter = Assert.IsType<ActiveChannelResponse>(((OkObjectResult)statusAfter.Result!).Value);
        Assert.False(respAfter.IsActive);

        // Other user 3 is NOT dismissed
        var user3Id = Guid.NewGuid();
        var controllerUser3 = CreateController(manager, user3Id);
        var statusUser3 = controllerUser3.GetActiveChannel();
        var respUser3 = Assert.IsType<ActiveChannelResponse>(((OkObjectResult)statusUser3.Result!).Value);
        Assert.True(respUser3.IsActive);
    }
}
