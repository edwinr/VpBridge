using System.Drawing;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VPLink.Common.Configuration;
using VPLink.Common.Data;
using VPLink.Common.Services;
using VpSharp;
using VpSharp.Entities;
using FontStyle = VpSharp.FontStyle;

namespace VPLink.Services;

/// <inheritdoc cref="IVirtualParadiseMessageService" />
internal sealed class VirtualParadiseMessageService : BackgroundService, IVirtualParadiseMessageService
{
    private readonly ILogger<VirtualParadiseMessageService> _logger;
    private readonly IConfigurationService _configurationService;
    private readonly VirtualParadiseClient _virtualParadiseClient;
    private readonly Subject<RelayedMessage> _messageReceived = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="VirtualParadiseMessageService" /> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configurationService">The configuration service.</param>
    /// <param name="virtualParadiseClient">The Virtual Paradise client.</param>
    public VirtualParadiseMessageService(ILogger<VirtualParadiseMessageService> logger,
        IConfigurationService configurationService,
        VirtualParadiseClient virtualParadiseClient)
    {
        _logger = logger;
        _configurationService = configurationService;
        _virtualParadiseClient = virtualParadiseClient;
    }

    /// <inheritdoc />
    public IObservable<RelayedMessage> OnMessageReceived => _messageReceived.AsObservable();

    /// <inheritdoc />
    public Task SendMessageAsync(RelayedMessage message)
    {
        IChatConfiguration configuration = _configurationService.VirtualParadiseConfiguration.Chat;

        Color color = Color.FromArgb((int)configuration.Color);
        FontStyle style = configuration.Style;
        return _virtualParadiseClient.SendMessageAsync(message.Author, message.Content, style, color);
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _virtualParadiseClient.MessageReceived.Subscribe(OnVPMessageReceived);
        return Task.CompletedTask;
    }

    private void OnVPMessageReceived(VirtualParadiseMessage message)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));
        if (message.Type != MessageType.ChatMessage) return;
        if (message.Author == _virtualParadiseClient.CurrentAvatar) return;
        if (message.Author.IsBot && !_configurationService.BotConfiguration.RelayBotMessages) return;

        _logger.LogInformation("Message by {Author}: {Content}", message.Author, message.Content);

        var relayedMessage = new RelayedMessage(message.Author.Name, message.Content);
        _messageReceived.OnNext(relayedMessage);
    }
}
