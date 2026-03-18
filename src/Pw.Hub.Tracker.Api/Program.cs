using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<TrackerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
