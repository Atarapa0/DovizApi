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
    public DbSet<MusteriHesabi> MusteriHesaplari => Set<MusteriHesabi>();
    public DbSet<KurKaydi> KurKayitlari => Set<KurKaydi>();
    public DbSet<DovizIslemi> DovizIslemleri => Set<DovizIslemi>();
    public DbSet<HesapHareketi> HesapHareketleri => Set<HesapHareketi>();
    public DbSet<HataLogu> HataLoglari => Set<HataLogu>();

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
            entity.Property(x => x.GuncellemeTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => x.SubeId);
            entity.HasOne(x => x.Sube)
                .WithMany(x => x.Musteriler)
                .HasForeignKey(x => x.SubeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MusteriHesabi>(entity =>
        {
            entity.ToTable("MusteriHesaplari", table =>
            {
                table.HasCheckConstraint("CK_MusteriHesaplari_HesapEkNo", "[HesapEkNo] >= 5001");
                table.HasCheckConstraint("CK_MusteriHesaplari_Bakiye", "[Bakiye] >= 0");
            });
            entity.HasKey(x => new { x.MusteriId, x.HesapEkNo });
            entity.Property(x => x.Bakiye).HasPrecision(19, 4);
            entity.Property(x => x.AktifMi).HasDefaultValue(true);
            entity.Property(x => x.OlusturmaTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.GuncellemeTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => x.DovizId);
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
            entity.ToTable("DovizIslemleri", table =>
            {
                table.HasCheckConstraint(
                    "CK_DovizIslemleri_Hesaplar",
                    "[BorcluHesapEkNo] <> [AlacakliHesapEkNo]");
                table.HasCheckConstraint(
                    "CK_DovizIslemleri_Dovizler",
                    "[OdenenDovizId] <> [AlinanDovizId]");
                table.HasCheckConstraint(
                    "CK_DovizIslemleri_Tutarlar",
                    "[OdenenDovizMiktari] > 0 AND [AlinanDovizMiktari] > 0 " +
                    "AND [OdenenDovizKuru] > 0 AND [AlinanDovizKuru] > 0 AND [TlKarsiligi] > 0");
                table.HasCheckConstraint(
                    "CK_DovizIslemleri_ReferansNo",
                    "LEFT([ReferansNo], 4) NOT LIKE '%[^0-9]%' " +
                    "AND SUBSTRING([ReferansNo], 5, 4) IN ('DOVA', 'DOVS') " +
                    "AND SUBSTRING([ReferansNo], 9, 2) NOT LIKE '%[^0-9]%' " +
                    "AND RIGHT([ReferansNo], 6) NOT LIKE '%[^0-9]%'");
                table.HasCheckConstraint(
                    "CK_DovizIslemleri_TersKayit",
                    "([OrijinalIslemId] IS NULL AND [IptalNedeni] IS NULL) " +
                    "OR ([OrijinalIslemId] IS NOT NULL AND [IptalNedeni] IS NOT NULL)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReferansNo)
                .HasMaxLength(16)
                .IsUnicode(false)
                .IsFixedLength()
                .IsRequired();
            entity.Property(x => x.OdenenDovizMiktari).HasPrecision(19, 4);
            entity.Property(x => x.AlinanDovizMiktari).HasPrecision(19, 4);
            entity.Property(x => x.OdenenDovizKuru).HasPrecision(19, 6);
            entity.Property(x => x.AlinanDovizKuru).HasPrecision(19, 6);
            entity.Property(x => x.TlKarsiligi).HasPrecision(19, 4);
            entity.Property(x => x.IslemTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.IptalNedeni).HasMaxLength(500);
            entity.HasIndex(x => x.ReferansNo).IsUnique();
            entity.HasIndex(x => x.OrijinalIslemId)
                .IsUnique()
                .HasFilter("[OrijinalIslemId] IS NOT NULL");
            entity.HasIndex(x => new { x.IslemTarihi, x.Id });
            entity.HasIndex(x => new { x.MusteriId, x.BorcluHesapEkNo });
            entity.HasIndex(x => new { x.MusteriId, x.AlacakliHesapEkNo });
            entity.HasIndex(x => x.OdenenDovizId);
            entity.HasIndex(x => x.AlinanDovizId);
            entity.HasOne(x => x.BorcluHesap)
                .WithMany(x => x.BorcluOlduguIslemler)
                .HasForeignKey(x => new { x.MusteriId, x.BorcluHesapEkNo })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AlacakliHesap)
                .WithMany(x => x.AlacakliOlduguIslemler)
                .HasForeignKey(x => new { x.MusteriId, x.AlacakliHesapEkNo })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.OdenenDoviz)
                .WithMany(x => x.OdenenOlduguIslemler)
                .HasForeignKey(x => x.OdenenDovizId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AlinanDoviz)
                .WithMany(x => x.AlinanOlduguIslemler)
                .HasForeignKey(x => x.AlinanDovizId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.OrijinalIslem)
                .WithOne(x => x.TersKayit)
                .HasForeignKey<DovizIslemi>(x => x.OrijinalIslemId)
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
            entity.HasIndex(x => new { x.MusteriId, x.HesapEkNo });
            entity.HasOne(x => x.DovizIslemi)
                .WithMany(x => x.HesapHareketleri)
                .HasForeignKey(x => x.DovizIslemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Hesap)
                .WithMany(x => x.Hareketler)
                .HasForeignKey(x => new { x.MusteriId, x.HesapEkNo })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HataLogu>(entity =>
        {
            entity.ToTable("HataLoglari");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.HataId).HasMaxLength(32).IsUnicode(false).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(128).IsUnicode(false).IsRequired();
            entity.Property(x => x.Tarih).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.Seviye).HasMaxLength(16).IsUnicode(false).IsRequired();
            entity.Property(x => x.HataKodu).HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(x => x.Mesaj).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Detay).HasColumnType("nvarchar(max)");
            entity.Property(x => x.StackTrace).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ExceptionTipi).HasMaxLength(500);
            entity.Property(x => x.Endpoint).HasMaxLength(2048);
            entity.Property(x => x.HttpMethod).HasMaxLength(16).IsUnicode(false);
            entity.Property(x => x.QueryString).HasMaxLength(2048);
            entity.Property(x => x.KullaniciId).HasMaxLength(256);
            entity.Property(x => x.SubeKodu).HasMaxLength(20);
            entity.Property(x => x.Ortam).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Kaynak).HasMaxLength(100).IsRequired();
            entity.Property(x => x.RequestBody).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => x.HataId).IsUnique();
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => x.Tarih);
            entity.HasIndex(x => x.HttpStatus);
            entity.HasIndex(x => x.HataKodu);
        });
    }
}
