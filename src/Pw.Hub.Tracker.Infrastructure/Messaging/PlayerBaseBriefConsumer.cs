using MassTransit;
using Microsoft.Extensions.Logging;
using Pw.Hub.Tracker.Sync.Web.Models;
using Pw.Hub.Tracker.Infrastructure.Processing;

namespace Pw.Hub.Tracker.Infrastructure.Messaging;

public class PlayerBaseBriefConsumer(
    PlayerBaseBriefProcessor processor,
    ILogger<PlayerBaseBriefConsumer> logger) : IConsumer<PlayerBaseBriefMessage>
{
    public async Task Consume(ConsumeContext<PlayerBaseBriefMessage> context)
    {
        var message = context.Message;

        logger.LogDebug("Processing player base brief message for role {RoleId} on server {Server}",
            message.RoleId, message.Server);

        await processor.ProcessAsync(message);
    }
}
