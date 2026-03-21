using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Domain.Entities;

namespace Pw.Hub.Tracker.Infrastructure.Data;

public class TrackerDbContext(DbContextOptions<TrackerDbContext> options) : DbContext(options)
{
    public DbSet<ArenaTeam> ArenaTeams => Set<ArenaTeam>();
    public DbSet<ArenaTeamMember> ArenaTeamMembers => Set<ArenaTeamMember>();
    public DbSet<ArenaPlayer> ArenaPlayers => Set<ArenaPlayer>();
    public DbSet<ArenaBattleStats> ArenaBattleStats => Set<ArenaBattleStats>();
    public DbSet<ArenaScoreHistory> ArenaScoreHistory => Set<ArenaScoreHistory>();
    public DbSet<ArenaMatch> ArenaMatches => Set<ArenaMatch>();
    public DbSet<ArenaMatchParticipant> ArenaMatchParticipants => Set<ArenaMatchParticipant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.Entity<ArenaTeam>(e =>
        {
            e.ToTable("arena_teams");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<ArenaTeamMember>(e =>
        {
            e.ToTable("arena_team_members");
            e.HasKey(x => new { x.TeamId, x.PlayerId });
            e.HasOne(x => x.Team).WithMany(t => t.Members).HasForeignKey(x => x.TeamId);
            e.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId);
        });

        modelBuilder.Entity<ArenaPlayer>(e =>
        {
            e.ToTable("arena_players");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId);
        });

        modelBuilder.Entity<ArenaBattleStats>(e =>
        {
            e.ToTable("arena_battle_stats");
            e.HasKey(x => new { x.EntityId, x.EntityType, x.MatchPattern });
        });

        modelBuilder.Entity<ArenaScoreHistory>(e =>
        {
            e.ToTable("arena_score_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Ignore(x => x.NormalizedScore);
            e.HasIndex(x => new { x.EntityId, x.EntityType, x.MatchPattern, x.RecordedAt });
        });

        modelBuilder.Entity<ArenaMatch>(e =>
        {
            e.ToTable("arena_matches");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasOne(x => x.TeamA).WithMany().HasForeignKey(x => x.TeamAId);
            e.HasOne(x => x.TeamB).WithMany().HasForeignKey(x => x.TeamBId);
            e.HasIndex(x => x.TeamAId);
            e.HasIndex(x => x.TeamBId);
            e.HasIndex(x => x.WinnerTeamId);
        });

        modelBuilder.Entity<ArenaMatchParticipant>(e =>
        {
            e.ToTable("arena_match_participants");
            e.HasKey(x => new { x.MatchId, x.PlayerId });
            e.HasOne(x => x.Match).WithMany(m => m.Participants).HasForeignKey(x => x.MatchId);
            e.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId);
            e.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId);
            e.HasIndex(x => x.PlayerId);
            e.HasIndex(x => new { x.MatchId, x.TeamId });
        });
    }
}
