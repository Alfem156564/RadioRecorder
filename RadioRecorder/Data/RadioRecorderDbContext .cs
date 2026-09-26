using Microsoft.EntityFrameworkCore;
using RadioRecorder.Models;
using System.Text.Json;

namespace RadioRecorder.Data;

public class RadioRecorderDbContext : DbContext
{
    public DbSet<RadioStation> RadioStations =>
        Set<RadioStation>();

    public DbSet<RadioProgram> RadioPrograms =>
        Set<RadioProgram>();

    public DbSet<Recording> Recordings =>
        Set<Recording>();

    public DbSet<ProgramHistory> ProgramHistory =>
        Set<ProgramHistory>();

    public RadioRecorderDbContext(
        DbContextOptions<RadioRecorderDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //
        // RadioStation
        //

        modelBuilder.Entity<RadioStation>()
            .HasKey(station => station.Id);

        modelBuilder.Entity<RadioStation>()
            .Property(station => station.Name)
            .IsRequired();

        modelBuilder.Entity<RadioStation>()
            .Property(station => station.Frequency)
            .IsRequired();

        modelBuilder.Entity<RadioStation>()
            .Property(station => station.City)
            .IsRequired();

        modelBuilder.Entity<RadioStation>()
            .Property(station => station.StreamUrl)
            .IsRequired();

        //
        // RadioProgram
        //

        modelBuilder.Entity<RadioProgram>()
            .HasKey(program => program.Id);

        modelBuilder.Entity<RadioProgram>()
            .Property(program => program.Name)
            .IsRequired();

        modelBuilder.Entity<RadioProgram>()
            .Property(program => program.Days)
            .HasConversion(
                days => JsonSerializer.Serialize(
                    days,
                    (JsonSerializerOptions?)null),

                json => JsonSerializer.Deserialize<List<DayOfWeek>>(
                    json,
                    (JsonSerializerOptions?)null) ?? new List<DayOfWeek>());

        modelBuilder.Entity<RadioProgram>()
            .HasOne(program => program.Station)
            .WithMany()
            .OnDelete(DeleteBehavior.Cascade);

        //
        // Recording
        //

        modelBuilder.Entity<Recording>()
            .HasKey(recording => recording.Id);

        modelBuilder.Entity<Recording>()
            .Property(recording => recording.FilePath)
            .IsRequired();

        modelBuilder.Entity<Recording>()
            .HasOne(recording => recording.Station)
            .WithMany()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Recording>()
            .HasOne(recording => recording.Program)
            .WithMany()
            .OnDelete(DeleteBehavior.SetNull);

        //
        // ProgramHistory
        //

        modelBuilder.Entity<ProgramHistory>()
            .HasKey(history => history.Id);

        modelBuilder.Entity<ProgramHistory>()
            .Property(history => history.DetectedName)
            .IsRequired();

        modelBuilder.Entity<ProgramHistory>()
            .HasOne(history => history.Station)
            .WithMany()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProgramHistory>()
            .HasOne(history => history.Program)
            .WithMany()
            .OnDelete(DeleteBehavior.SetNull);
    }
}