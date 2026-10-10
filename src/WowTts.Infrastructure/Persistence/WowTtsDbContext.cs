using Microsoft.EntityFrameworkCore;

namespace WowTts.Infrastructure.Persistence;

public sealed class WowTtsDbContext(DbContextOptions<WowTtsDbContext> options) : DbContext(options)
{
    public DbSet<QuestEntity> Quests => Set<QuestEntity>();
    public DbSet<MapZoneEntity> MapZones => Set<MapZoneEntity>();
    public DbSet<QuestCategoryEntity> QuestCategories => Set<QuestCategoryEntity>();
    public DbSet<NpcVoiceEntity> NpcVoices => Set<NpcVoiceEntity>();
    public DbSet<AudioPerformanceEntity> AudioPerformances => Set<AudioPerformanceEntity>();
    public DbSet<AudioTimingsEntity> AudioTimings => Set<AudioTimingsEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuestEntity>(quest =>
        {
            quest.ToTable("Quests");
            quest.HasKey(q => q.Id);

            // Quest IDs come from the game, so the database must not generate them.
            quest.Property(q => q.Id).ValueGeneratedNever();

            // Stored as text ("CMangos", "Alliance"), so the database is readable in a SQLite browser.
            quest.Property(q => q.Source).HasConversion<string>().HasMaxLength(20);
            quest.Property(q => q.Side).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<MapZoneEntity>(zone =>
        {
            zone.ToTable("MapZones");
            zone.HasKey(z => z.Id);
            zone.Property(z => z.Id).ValueGeneratedNever();
            zone.Property(z => z.Type).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<QuestCategoryEntity>(category =>
        {
            category.ToTable("QuestCategories");
            category.HasKey(c => c.Id);
            category.Property(c => c.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<NpcVoiceEntity>(voice =>
        {
            voice.ToTable("NpcVoices");
            voice.HasKey(v => v.Id);
            voice.HasIndex(v => new { v.NpcName, v.NpcGender }).IsUnique();
        });

        modelBuilder.Entity<AudioPerformanceEntity>(performance =>
        {
            performance.ToTable("AudioPerformances");
            performance.HasKey(p => new { p.QuestId, p.Part });
            performance.Property(p => p.Part).HasConversion<string>().HasMaxLength(20);
            performance.Property(p => p.DirectedBy).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<AudioTimingsEntity>(timings =>
        {
            timings.ToTable("AudioTimings");
            timings.HasKey(t => new { t.QuestId, t.Part });
            timings.Property(t => t.Part).HasConversion<string>().HasMaxLength(20);
            timings.Property(t => t.Source).HasConversion<string>().HasMaxLength(20);
        });
    }
}
