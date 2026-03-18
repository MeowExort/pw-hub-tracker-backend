using MassTransit;
using Microsoft.Extensions.Logging;
using Pw.Hub.Tracker.Sync.Web.Models;
using Pw.Hub.Tracker.Infrastructure.Processing;

namespace Pw.Hub.Tracker.Infrastructure.Messaging;

public class ArenaConsumer(
    ArenaMessageProcessor processor,
    ILogger<ArenaConsumer> logger) : IConsumer<ArenaMessage>
{
    private const int ArenaOpcode = 5665;

    public async Task Consume(ConsumeContext<ArenaMessage> context)
    {
        var message = context.Message;
        var data = message.Data;

        if (data.Opcode != ArenaOpcode)
            return;

        logger.LogDebug("Processing arena message from server {Server}, roleid {RoleId}",
            message.Server, data.Roleid);

        await processor.ProcessAsync(data);
    }
}
