using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pw.Hub.Tracker.Infrastructure.Cache;
using Pw.Hub.Tracker.Infrastructure.Data;
using Pw.Hub.Tracker.Infrastructure.Messaging;
using Pw.Hub.Tracker.Infrastructure.Processing;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

var postgresConnectionString = new NpgsqlConnectionStringBuilder(
    builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=pw_hub_tracker;Username=postgres;Password=postgres")
    { MaxPoolSize = 20 }.ConnectionString;

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

var dataSource = new NpgsqlDataSourceBuilder(postgresConnectionString).Build();
builder.Services.AddSingleton(dataSource);

builder.Services.AddDbContext<TrackerDbContext>(options =>
    options.UseNpgsql(dataSource));
builder.Services.AddSingleton<ArenaStateCache>();
builder.Services.AddScoped<ArenaMessageProcessor>();
builder.Services.AddScoped<PlayerPropertyProcessor>();
builder.Services.AddScoped<PlayerBaseBriefProcessor>();

var rabbitSection = builder.Configuration.GetSection("RabbitMQ");

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ArenaConsumer>();
    x.AddConsumer<PlayerPropertyConsumer>();
    x.AddConsumer<PlayerBaseBriefConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitSection["Host"], h =>
        {
            h.Username(rabbitSection["Username"] ?? "guest");
            h.Password(rabbitSection["Password"] ?? "guest");
        });

        cfg.ConcurrentMessageLimit = 10;

        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
    await db.Database.MigrateAsync();
}

await host.RunAsync();
