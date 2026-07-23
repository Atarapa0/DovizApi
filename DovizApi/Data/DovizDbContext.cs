using DovizApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DovizApi.Data;

public class DovizDbContext : DbContext
{
    public DovizDbContext(DbContextOptions<DovizDbContext> options)
        : base(options)
    {
    }

    public DbSet<Doviz> Dovizler => Set<Doviz>();
    public DbSet<Musteri> Musteriler => Set<Musteri>();
    public DbSet<MusteriHesabi> MusteriHesaplari => Set<MusteriHesabi>();
    public DbSet<KurKaydi> KurKayitlari => Set<KurKaydi>();
    public DbSet<DovizIslemi> DovizIslemleri => Set<DovizIslemi>();
    public DbSet<HesapHareketi> HesapHareketleri => Set<HesapHareketi>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Doviz>(entity =>
        {
            entity.ToTable("Dovizler");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kod).HasMaxLength(3).IsUnicode(false).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Birim).HasDefaultValue((short)1);
            entity.Property(x => x.AktifMi).HasDefaultValue(true);
            entity.Property(x => x.OlusturmaTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => x.Kod).IsUnique();
        });

        modelBuilder.Entity<Musteri>(entity =>
        {
            entity.ToTable("Musteriler");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Ad).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Soyad).HasMaxLength(100).IsRequired();
            entity.Property(x => x.AktifMi).HasDefaultValue(true);
            entity.Property(x => x.OlusturmaTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<MusteriHesabi>(entity =>
        {
            entity.ToTable("MusteriHesaplari");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Bakiye).HasPrecision(19, 4);
            entity.Property(x => x.AktifMi).HasDefaultValue(true);
            entity.Property(x => x.OlusturmaTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.GuncellemeTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => new { x.MusteriId, x.EkNo }).IsUnique();
            entity.HasOne(x => x.Musteri)
                .WithMany(x => x.Hesaplar)
                .HasForeignKey(x => x.MusteriId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Doviz)
                .WithMany(x => x.MusteriHesaplari)
                .HasForeignKey(x => x.DovizId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KurKaydi>(entity =>
        {
            entity.ToTable("KurKayitlari");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KurTarihi).HasColumnType("date");
            entity.Property(x => x.AlisKuru).HasPrecision(19, 6);
            entity.Property(x => x.SatisKuru).HasPrecision(19, 6);
            entity.Property(x => x.OlusturmaTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => new { x.DovizId, x.KurTarihi }).IsUnique();
            entity.HasOne(x => x.Doviz)
                .WithMany(x => x.KurKayitlari)
                .HasForeignKey(x => x.DovizId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DovizIslemi>(entity =>
        {
            entity.ToTable("DovizIslemleri");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReferansNo).HasDefaultValueSql("NEWID()");
            entity.Property(x => x.OdenenDovizMiktari).HasPrecision(19, 4);
            entity.Property(x => x.AlinanDovizMiktari).HasPrecision(19, 4);
            entity.Property(x => x.OdenenDovizKuru).HasPrecision(19, 6);
            entity.Property(x => x.AlinanDovizKuru).HasPrecision(19, 6);
            entity.Property(x => x.TlKarsiligi).HasPrecision(19, 4);
            entity.Property(x => x.IslemTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => x.ReferansNo).IsUnique();
            entity.HasOne(x => x.Musteri)
                .WithMany(x => x.DovizIslemleri)
                .HasForeignKey(x => x.MusteriId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.BorcluHesap)
                .WithMany(x => x.BorcluOlduguIslemler)
                .HasForeignKey(x => x.BorcluHesapId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AlacakliHesap)
                .WithMany(x => x.AlacakliOlduguIslemler)
                .HasForeignKey(x => x.AlacakliHesapId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HesapHareketi>(entity =>
        {
            entity.ToTable("HesapHareketleri", table =>
                table.HasCheckConstraint(
                    "CK_HesapHareketleri_HareketTuru",
                    "[HareketTuru] IN ('BORC', 'ALACAK')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.HareketTuru).HasMaxLength(6).IsUnicode(false).IsRequired();
            entity.Property(x => x.DovizMiktari).HasPrecision(19, 4);
            entity.Property(x => x.TlKarsiligi).HasPrecision(19, 4);
            entity.Property(x => x.IslemTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(x => x.DovizIslemi)
                .WithMany(x => x.HesapHareketleri)
                .HasForeignKey(x => x.DovizIslemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Hesap)
                .WithMany(x => x.Hareketler)
                .HasForeignKey(x => x.HesapId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
