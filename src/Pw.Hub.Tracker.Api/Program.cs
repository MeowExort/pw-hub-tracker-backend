using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pw.Hub.Tracker.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5174", "https://tracker.pw-hub.ru")
            .AllowAnyHeader()
            .AllowAnyMethod()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var postgresConnectionString = new NpgsqlConnectionStringBuilder(
    builder.Configuration.GetConnectionString("Postgres")) { MaxPoolSize = 20 }.ConnectionString;

builder.Services.AddDbContext<TrackerDbContext>(options =>
    options.UseNpgsql(postgresConnectionString, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "swagger";
});

app.UseCors();

app.MapControllers();
app.Run();
