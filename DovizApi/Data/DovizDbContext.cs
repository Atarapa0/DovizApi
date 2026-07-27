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
    public DbSet<Sube> Subeler => Set<Sube>();
    public DbSet<Musteri> Musteriler => Set<Musteri>();
    public DbSet<AnaHesap> AnaHesaplar => Set<AnaHesap>();
    public DbSet<EkHesap> EkHesaplar => Set<EkHesap>();
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

        modelBuilder.Entity<Sube>(entity =>
        {
            entity.ToTable("Subeler");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kod).HasMaxLength(10).IsUnicode(false).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(100).IsRequired();
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

        modelBuilder.Entity<AnaHesap>(entity =>
        {
            entity.ToTable("AnaHesaplar", table =>
                table.HasCheckConstraint(
                    "CK_AnaHesaplar_HesapNo",
                    "[HesapNo] NOT LIKE '%[^0-9]%' AND LEN([HesapNo]) = 10"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.HesapNo).HasMaxLength(10).IsUnicode(false).IsRequired();
            entity.Property(x => x.AktifMi).HasDefaultValue(true);
            entity.Property(x => x.OlusturmaTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.GuncellemeTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => x.HesapNo).IsUnique();
            entity.HasIndex(x => x.MusteriId);
            entity.HasIndex(x => x.SubeId);
            entity.HasOne(x => x.Musteri)
                .WithMany(x => x.AnaHesaplar)
                .HasForeignKey(x => x.MusteriId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Sube)
                .WithMany(x => x.AnaHesaplar)
                .HasForeignKey(x => x.SubeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EkHesap>(entity =>
        {
            entity.ToTable("EkHesaplar", table =>
                table.HasCheckConstraint("CK_EkHesaplar_Bakiye", "[Bakiye] >= 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Bakiye).HasPrecision(19, 4);
            entity.Property(x => x.AktifMi).HasDefaultValue(true);
            entity.Property(x => x.OlusturmaTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.GuncellemeTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => new { x.AnaHesapId, x.EkNo }).IsUnique();
            entity.HasIndex(x => x.DovizId);
            entity.HasOne(x => x.AnaHesap)
                .WithMany(x => x.EkHesaplar)
                .HasForeignKey(x => x.AnaHesapId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Doviz)
                .WithMany(x => x.EkHesaplar)
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
            entity.ToTable("DovizIslemleri", table =>
            {
                table.HasCheckConstraint(
                    "CK_DovizIslemleri_Hesaplar",
                    "[BorcluHesapId] <> [AlacakliHesapId]");
                table.HasCheckConstraint(
                    "CK_DovizIslemleri_Dovizler",
                    "[OdenenDovizId] <> [AlinanDovizId]");
                table.HasCheckConstraint(
                    "CK_DovizIslemleri_Tutarlar",
                    "[OdenenDovizMiktari] > 0 AND [AlinanDovizMiktari] > 0 " +
                    "AND [OdenenDovizKuru] > 0 AND [AlinanDovizKuru] > 0 AND [TlKarsiligi] > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReferansNo).HasDefaultValueSql("NEWID()");
            entity.Property(x => x.OdenenDovizMiktari).HasPrecision(19, 4);
            entity.Property(x => x.AlinanDovizMiktari).HasPrecision(19, 4);
            entity.Property(x => x.OdenenDovizKuru).HasPrecision(19, 6);
            entity.Property(x => x.AlinanDovizKuru).HasPrecision(19, 6);
            entity.Property(x => x.TlKarsiligi).HasPrecision(19, 4);
            entity.Property(x => x.IslemTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => x.ReferansNo).IsUnique();
            entity.HasIndex(x => x.BorcluHesapId);
            entity.HasIndex(x => x.AlacakliHesapId);
            entity.HasIndex(x => x.OdenenDovizId);
            entity.HasIndex(x => x.AlinanDovizId);
            entity.HasOne(x => x.BorcluHesap)
                .WithMany(x => x.BorcluOlduguIslemler)
                .HasForeignKey(x => x.BorcluHesapId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AlacakliHesap)
                .WithMany(x => x.AlacakliOlduguIslemler)
                .HasForeignKey(x => x.AlacakliHesapId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.OdenenDoviz)
                .WithMany(x => x.OdenenOlduguIslemler)
                .HasForeignKey(x => x.OdenenDovizId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AlinanDoviz)
                .WithMany(x => x.AlinanOlduguIslemler)
                .HasForeignKey(x => x.AlinanDovizId)
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
            entity.HasIndex(x => x.DovizIslemId);
            entity.HasIndex(x => x.HesapId);
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
