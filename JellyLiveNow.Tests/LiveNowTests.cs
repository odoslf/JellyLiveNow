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
    private readonly Mock<ISessionManager> _sessionManagerMock = new();
    private readonly Mock<ILibraryManager> _libraryManagerMock = new();
    private readonly Mock<ILiveTvManager> _liveTvManagerMock = new();
    private readonly Mock<IMediaSourceManager> _mediaSourceManagerMock = new();
    private readonly Mock<ILogger<LiveNowManager>> _managerLoggerMock = new();
    private readonly Mock<ILogger<JellyLiveChannel>> _channelLoggerMock = new();
    private readonly Mock<ILogger> _sessionLoggerMock = new();

    public LiveNowTests()
    {
        var appPathsMock = new Mock<IApplicationPaths>();
        appPathsMock.Setup(a => a.PluginsPath).Returns("/tmp/plugins");
        appPathsMock.Setup(a => a.PluginConfigurationsPath).Returns("/tmp/pluginconfigs");
        _ = new Plugin(appPathsMock.Object, new Mock<IXmlSerializer>().Object);
        Plugin.Instance!.UpdateConfiguration(new PluginConfiguration());
    }

    private LiveNowManager CreateManager() => new(
        _sessionManagerMock.Object, _libraryManagerMock.Object, _liveTvManagerMock.Object, _managerLoggerMock.Object);

    private JellyLiveChannel CreateChannel(LiveNowManager manager) => new(
        manager, _libraryManagerMock.Object, _mediaSourceManagerMock.Object, _channelLoggerMock.Object);

    private SessionInfo CreateSession(string id, BaseItem item, Guid userId = default) => new(_sessionManagerMock.Object, _sessionLoggerMock.Object)
    {
        Id = id,
        FullNowPlayingItem = item,
        UserId = userId == default ? Guid.NewGuid() : userId
    };

    private JellyLiveNowController CreateController(LiveNowManager manager, Guid userId)
    {
        var controller = new JellyLiveNowController(manager);
        var context = new DefaultHttpContext();
        if (userId != Guid.Empty)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "TestAuth"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    [Fact]
    public async Task NoLiveTvSession_ChannelDisabledAndStatusInactive()
    {
        _sessionManagerMock.Setup(s => s.Sessions).Returns(new List<SessionInfo>());
        var manager = CreateManager();
        var channel = CreateChannel(manager);
        Assert.False(channel.IsEnabledFor("user1"));
        Assert.Empty((await channel.GetChannelItems(new InternalChannelItemQuery(), CancellationToken.None)).Items);
        var result = CreateController(manager, Guid.NewGuid()).GetActiveChannel();
        Assert.False(Assert.IsType<ActiveChannelResponse>(Assert.IsType<OkObjectResult>(result.Result).Value).IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NonLivePlayback_DoesNotActivate(bool movie)
    {
        BaseItem item = movie ? new Movie { Id = Guid.NewGuid(), Name = "Movie" } : new Episode { Id = Guid.NewGuid(), Name = "Episode" };
        _sessionManagerMock.Setup(s => s.Sessions).Returns(new[] { CreateSession("s1", item) });
        var manager = CreateManager();
        manager.RefreshActiveChannelState();
        Assert.Equal(Guid.Empty, manager.ActiveChannelId);
    }

    [Fact]
    public async Task LiveTvPlayback_ProducesSingleNativeItem()
    {
        var id = Guid.NewGuid();
        var live = new LiveTvChannel { Id = id, Name = "Movistar Fútbol" };
        _sessionManagerMock.Setup(s => s.Sessions).Returns(new[] { CreateSession("s1", live), CreateSession("s2", live) });
        _libraryManagerMock.Setup(l => l.GetItemById(id)).Returns(live);
        var manager = CreateManager();
        var channel = CreateChannel(manager);
        Assert.True(channel.IsEnabledFor("user1"));
        var result = await channel.GetChannelItems(new InternalChannelItemQuery(), CancellationToken.None);
        Assert.Single(result.Items);
        Assert.Equal(id.ToString("N"), result.Items[0].Id);
        Assert.True(result.Items[0].IsLiveStream);
    }

    [Fact]
    public async Task PlaybackCallback_UsesJellyfinPlaybackMediaSources()
    {
        var id = Guid.NewGuid();
        var live = new LiveTvChannel { Id = id, Name = "Live" };
        var sources = new List<MediaSourceInfo> { new() { Id = "dynamic-live-source", OpenToken = "token" } };
        _libraryManagerMock.Setup(l => l.GetItemById(id)).Returns(live);
        _mediaSourceManagerMock
            .Setup(m => m.GetPlaybackMediaSources(live, null!, false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sources);
        var result = await CreateChannel(CreateManager()).GetChannelItemMediaInfo(id.ToString("N"), CancellationToken.None);
        Assert.Single(result);
        _mediaSourceManagerMock.Verify(
            m => m.GetPlaybackMediaSources(live, null!, false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChannelImage_ReturnsExplicitNoImageResponse()
    {
        var response = await CreateChannel(CreateManager()).GetChannelImage(ImageType.Primary, CancellationToken.None);
        Assert.NotNull(response);
        Assert.False(response.HasImage);
    }

    [Fact]
    public void Dismiss_IsPerUserAndResetsWhenChannelChanges()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var first = new LiveTvChannel { Id = firstId, Name = "One" };
        var second = new LiveTvChannel { Id = secondId, Name = "Two" };
        _libraryManagerMock.Setup(l => l.GetItemById(firstId)).Returns(first);
        _libraryManagerMock.Setup(l => l.GetItemById(secondId)).Returns(second);
        _sessionManagerMock.Setup(s => s.Sessions).Returns(new[] { CreateSession("s1", first) });
        var manager = CreateManager();
        manager.RefreshActiveChannelState();
        var user = Guid.NewGuid();
        manager.DismissForUser(user);
        Assert.True(manager.IsDismissedForUser(user));
        _sessionManagerMock.Setup(s => s.Sessions).Returns(new[] { CreateSession("s2", second) });
        manager.RefreshActiveChannelState();
        Assert.False(manager.IsDismissedForUser(user));
    }
}
