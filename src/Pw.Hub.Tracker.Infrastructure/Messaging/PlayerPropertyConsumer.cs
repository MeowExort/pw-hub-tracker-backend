using MassTransit;
using Microsoft.Extensions.Logging;
using Pw.Hub.Tracker.Sync.Web.Models;
using Pw.Hub.Tracker.Infrastructure.Processing;

namespace Pw.Hub.Tracker.Infrastructure.Messaging;

public class PlayerPropertyConsumer(
    PlayerPropertyProcessor processor,
    ILogger<PlayerPropertyConsumer> logger) : IConsumer<PlayerPropertyMessage>
{
    public async Task Consume(ConsumeContext<PlayerPropertyMessage> context)
    {
        var message = context.Message;

        logger.LogDebug("Processing player property message for player {PlayerId} on server {Server}",
            message.PlayerId, message.Server);

        await processor.ProcessAsync(message);
    }
}
