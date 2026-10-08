using Microsoft.EntityFrameworkCore;
using RadioRecorder.Data.Entities;

namespace RadioRecorder.Data;

public class RadioDbContext : DbContext
{
    public RadioDbContext(DbContextOptions<RadioDbContext> options)
        : base(options)
    {
    }

    public DbSet<CityEntity> Cities => Set<CityEntity>();

    public DbSet<RadioStationEntity> RadioStations => Set<RadioStationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CityEntity>(entity =>
        {
            entity.ToTable("Ciudad");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("Id");

            entity.Property(x => x.Nombre)
                .HasColumnName("Nombre")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Estado)
                .HasColumnName("Estado")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Pais)
                .HasColumnName("Pais")
                .HasMaxLength(100)
                .IsRequired();
        });

        modelBuilder.Entity<RadioStationEntity>(entity =>
        {
            entity.ToTable("EstacionDeRadio");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Nombre)
                .HasColumnName("Nombre")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Frecuencia)
                .HasColumnName("Frecuencia")
                .HasMaxLength(15);

            entity.Property(x => x.CiudadId)
                .HasColumnName("CiudadId")
                .IsRequired();

            entity.Property(x => x.PaginaWeb)
                .HasColumnName("PaginaWeb")
                .HasMaxLength(500);

            entity.Property(x => x.UrlStream)
                .HasColumnName("UrlStream")
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(x => x.FormatoStream)
                .HasColumnName("FormatoStream")
                .HasMaxLength(20);

            entity.Property(x => x.HorarioInicio)
                .HasColumnName("HorarioInicio");

            entity.Property(x => x.HorarioFin)
                .HasColumnName("HorarioFin");

            entity.Property(x => x.Activa)
                .HasColumnName("Activa")
                .IsRequired();

            entity.HasOne(x => x.Ciudad)
                .WithMany(x => x.Estaciones)
                .HasForeignKey(x => x.CiudadId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}