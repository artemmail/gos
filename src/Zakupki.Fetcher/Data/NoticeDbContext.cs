using System;
using System.Linq;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Zakupki.Fetcher.Data.Entities;

namespace Zakupki.Fetcher.Data;

public class NoticeDbContext : IdentityDbContext<ApplicationUser>
{
    private const int NoticeEmbeddingVectorDimensions = 768;

    private static readonly ValueConverter<double[], byte[]> NoticeEmbeddingVectorConverter =
        new(
            v => ConvertVectorToBytes(v ?? Array.Empty<double>()),
            v => ConvertBytesToVector(v ?? Array.Empty<byte>()));

    private static readonly ValueComparer<double[]> NoticeEmbeddingVectorComparer = new(
        (left, right) =>
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.SequenceEqual(right);
        },
        vector =>
        {
            if (vector is null)
            {
                return 0;
            }

            var hash = new HashCode();
            foreach (var value in vector)
            {
                hash.Add(value);
            }

            return hash.ToHashCode();
        },
        vector => vector is null ? Array.Empty<double>() : vector.ToArray());

    public NoticeDbContext(DbContextOptions<NoticeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Notice> Notices => Set<Notice>();

    public DbSet<NoticeVersion> NoticeVersions => Set<NoticeVersion>();

    public DbSet<Contract> Contracts => Set<Contract>();

    public DbSet<ProcedureWindow> ProcedureWindows => Set<ProcedureWindow>();

    public DbSet<NoticeAttachment> NoticeAttachments => Set<NoticeAttachment>();

    public DbSet<AttachmentSignature> AttachmentSignatures => Set<AttachmentSignature>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<NoticeSearchVector> NoticeSearchVectors => Set<NoticeSearchVector>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<ApplicationUserRegion> ApplicationUserRegions => Set<ApplicationUserRegion>();

    public DbSet<NoticeAnalysis> NoticeAnalyses => Set<NoticeAnalysis>();

    public DbSet<NoticeEmbedding> NoticeEmbeddings => Set<NoticeEmbedding>();

    public DbSet<FavoriteNotice> FavoriteNotices => Set<FavoriteNotice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureNotice(modelBuilder);
        ConfigureNoticeVersion(modelBuilder);
        ConfigureContract(modelBuilder);
        ConfigureProcedureWindow(modelBuilder);
        ConfigureNoticeAttachment(modelBuilder);
        ConfigureAttachmentSignature(modelBuilder);
        ConfigureImportBatch(modelBuilder);
        ConfigureNoticeSearchVector(modelBuilder);
        ConfigureRefreshToken(modelBuilder);
        ConfigureApplicationUser(modelBuilder);
        ConfigureNoticeAnalysis(modelBuilder);
        ConfigureNoticeEmbedding(modelBuilder);
        ConfigureFavoriteNotice(modelBuilder);
    }

    private static void ConfigureApplicationUser(ModelBuilder modelBuilder)
    {
        var userEntity = modelBuilder.Entity<ApplicationUser>();
        userEntity.Property(u => u.CompanyInfo).HasColumnType("nvarchar(max)");

        userEntity
            .HasMany(u => u.Regions)
            .WithOne(r => r.User)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        userEntity
            .HasMany(u => u.NoticeAnalyses)
            .WithOne(a => a.User)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        userEntity
            .HasMany(u => u.FavoriteNotices)
            .WithOne(f => f.User)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var regionEntity = modelBuilder.Entity<ApplicationUserRegion>();
        regionEntity.ToTable("ApplicationUserRegions");
        regionEntity.HasKey(r => r.Id);
        regionEntity.Property(r => r.Region).HasMaxLength(128);
        regionEntity.HasIndex(r => new { r.UserId, r.Region }).IsUnique();
    }

    private static void ConfigureNotice(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Notice>();
        entity.ToTable("Notices");
        entity.HasKey(n => n.Id);

        entity.Property(n => n.Source).HasMaxLength(100);
        entity.Property(n => n.DocumentType).HasMaxLength(100);
        entity.Property(n => n.Region).HasMaxLength(128);
        entity.Property(n => n.Period).HasMaxLength(64);
        entity.Property(n => n.EntryName).HasMaxLength(256);
        entity.Property(n => n.ExternalId).HasMaxLength(128);
        entity.Property(n => n.SchemeVersion).HasMaxLength(64);
        entity.Property(n => n.PurchaseNumber).HasMaxLength(64);
        entity.Property(n => n.DocumentNumber).HasMaxLength(64);
        entity.Property(n => n.Href).HasMaxLength(512);
        entity.Property(n => n.PlacingWayCode).HasMaxLength(32);
        entity.Property(n => n.PlacingWayName).HasMaxLength(256);
        entity.Property(n => n.EtpCode).HasMaxLength(32);
        entity.Property(n => n.EtpName).HasMaxLength(256);
        entity.Property(n => n.EtpUrl).HasMaxLength(512);
        entity.Property(n => n.PurchaseObjectInfo).HasColumnType("nvarchar(max)");
        entity.Property(n => n.Article15FeaturesInfo).HasColumnType("nvarchar(max)");
        entity.Property(n => n.MaxPrice).HasColumnType("decimal(18,2)");
        entity.Property(n => n.MaxPriceCurrencyCode).HasMaxLength(32);
        entity.Property(n => n.MaxPriceCurrencyName).HasMaxLength(128);
        entity.Property(n => n.Okpd2Code).HasMaxLength(64);
        entity.Property(n => n.Okpd2Name).HasMaxLength(512);
        entity.Property(n => n.KvrCode).HasMaxLength(64);
        entity.Property(n => n.KvrName).HasMaxLength(512);
        entity.Property(n => n.RawJson).HasColumnType("nvarchar(max)");

        entity.HasIndex(n => n.PurchaseNumber).HasDatabaseName("IX_Notices_PurchaseNumber");
        entity.HasIndex(n => n.Period).HasDatabaseName("IX_Notices_Period");
        entity.HasIndex(n => n.CollectingEnd).HasDatabaseName("IX_Notices_CollectingEnd");

        entity.HasMany(n => n.Versions)
            .WithOne(v => v.Notice)
            .HasForeignKey(v => v.NoticeId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(n => n.Analyses)
            .WithOne(a => a.Notice)
            .HasForeignKey(a => a.NoticeId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(n => n.Favorites)
            .WithOne(f => f.Notice)
            .HasForeignKey(f => f.NoticeId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureFavoriteNotice(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FavoriteNotice>();
        entity.ToTable("FavoriteNotices");
        entity.HasKey(f => f.Id);

        entity.Property(f => f.UserId).HasMaxLength(450);
        entity.Property(f => f.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        entity.HasIndex(f => new { f.UserId, f.NoticeId })
            .IsUnique()
            .HasDatabaseName("UX_FavoriteNotices_User_Notice");
    }

    private static void ConfigureNoticeVersion(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NoticeVersion>();
        entity.ToTable("NoticeVersions");
        entity.HasKey(v => v.Id);

        entity.Property(v => v.ExternalId).HasMaxLength(128);
        entity.Property(v => v.Hash).HasMaxLength(128);
        entity.Property(v => v.SourceFileName).HasMaxLength(256);
        entity.Property(v => v.RawJson).HasColumnType("nvarchar(max)");

        entity.HasIndex(v => new { v.ExternalId, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("UX_NoticeVersions_External_Version");

        entity.HasOne(v => v.ImportBatch)
            .WithMany(b => b.NoticeVersions)
            .HasForeignKey(v => v.ImportBatchId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(v => v.ProcedureWindow)
            .WithOne(p => p.NoticeVersion)
            .HasForeignKey<ProcedureWindow>(p => p.NoticeVersionId);

        entity.HasOne(v => v.SearchVector)
            .WithOne(s => s.NoticeVersion)
            .HasForeignKey<NoticeSearchVector>(s => s.NoticeVersionId);
    }

    private static void ConfigureContract(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Contract>();
        entity.ToTable("Contracts");
        entity.HasKey(c => c.Id);

        entity.Property(c => c.Source).HasMaxLength(100);
        entity.Property(c => c.DocumentType).HasMaxLength(100);
        entity.Property(c => c.EntryName).HasMaxLength(256);
        entity.Property(c => c.Region).HasMaxLength(128);
        entity.Property(c => c.Period).HasMaxLength(64);
        entity.Property(c => c.ExternalId).HasMaxLength(128);
        entity.Property(c => c.RegNumber).HasMaxLength(128);
        entity.Property(c => c.Number).HasMaxLength(64);
        entity.Property(c => c.SchemeVersion).HasMaxLength(64);
        entity.Property(c => c.PurchaseNumber).HasMaxLength(64);
        entity.Property(c => c.LotNumber).HasMaxLength(64);
        entity.Property(c => c.ContractSubject).HasColumnType("nvarchar(max)");
        entity.Property(c => c.Price).HasColumnType("decimal(18,2)");
        entity.Property(c => c.CurrencyCode).HasMaxLength(32);
        entity.Property(c => c.CurrencyName).HasMaxLength(128);
        entity.Property(c => c.Okpd2Code).HasMaxLength(64);
        entity.Property(c => c.Okpd2Name).HasMaxLength(512);
        entity.Property(c => c.Href).HasMaxLength(512);
        entity.Property(c => c.RawJson).HasColumnType("nvarchar(max)");

        entity.HasIndex(c => c.ExternalId)
            .IsUnique()
            .HasDatabaseName("UX_Contracts_ExternalId");
    }

    private static void ConfigureProcedureWindow(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProcedureWindow>();
        entity.ToTable("ProcedureWindows");
        entity.HasKey(p => p.Id);

        entity.Property(p => p.BiddingDateRaw).HasMaxLength(64);
        entity.Property(p => p.SummarizingDateRaw).HasMaxLength(64);
        entity.Property(p => p.FirstPartsDateRaw).HasMaxLength(64);
        entity.Property(p => p.SubmissionProcedureDateRaw).HasMaxLength(64);
        entity.Property(p => p.SecondPartsDateRaw).HasMaxLength(64);

        entity.HasIndex(p => p.CollectingStart).HasDatabaseName("IX_ProcedureWindows_CollectingStart");
        entity.HasIndex(p => p.CollectingEnd).HasDatabaseName("IX_ProcedureWindows_CollectingEnd");
    }

    private static void ConfigureNoticeAttachment(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NoticeAttachment>();
        entity.ToTable("NoticeAttachments");
        entity.HasKey(a => a.Id);

        entity.Property(a => a.PublishedContentId).HasMaxLength(128);
        entity.Property(a => a.FileName).HasMaxLength(512);
        entity.Property(a => a.Description).HasColumnType("nvarchar(max)");
        entity.Property(a => a.DocumentKindCode).HasMaxLength(64);
        entity.Property(a => a.DocumentKindName).HasMaxLength(256);
        entity.Property(a => a.Url).HasMaxLength(512);
        entity.Property(a => a.ContentHash).HasMaxLength(128);
        entity.Property(a => a.BinaryContent).HasColumnType("varbinary(max)");
        entity.Property(a => a.MarkdownContent).HasColumnType("nvarchar(max)");
        entity.Property(a => a.SourceFileName).HasMaxLength(256);

        entity.HasIndex(a => a.FileName).HasDatabaseName("IX_NoticeAttachments_FileName");
        entity.HasIndex(a => a.DocumentKindCode).HasDatabaseName("IX_NoticeAttachments_DocKindCode");
        entity.HasIndex(a => new { a.PublishedContentId, a.NoticeVersionId })
            .IsUnique()
            .HasDatabaseName("UX_NoticeAttachments_ContentId_Version");

        entity.HasMany(a => a.Signatures)
            .WithOne(s => s.Attachment)
            .HasForeignKey(s => s.AttachmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureAttachmentSignature(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AttachmentSignature>();
        entity.ToTable("AttachmentSignatures");
        entity.HasKey(s => s.Id);

        entity.Property(s => s.SignatureType).HasMaxLength(64);
        entity.Property(s => s.SignatureValue).HasColumnType("nvarchar(max)");
    }

    private static void ConfigureNoticeAnalysis(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NoticeAnalysis>();
        entity.ToTable("NoticeAnalyses");
        entity.HasKey(a => a.Id);

        entity.Property(a => a.Status).HasMaxLength(32);
        entity.Property(a => a.Result).HasColumnType("nvarchar(max)");
        entity.Property(a => a.DecisionScore).HasColumnType("float");
        entity.Property(a => a.Recommended).HasColumnType("bit");
        entity.Property(a => a.Error).HasColumnType("nvarchar(max)");

        entity.HasIndex(a => new { a.NoticeId, a.UserId })
            .IsUnique()
            .HasDatabaseName("UX_NoticeAnalyses_Notice_User");
    }

    private static void ConfigureNoticeEmbedding(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NoticeEmbedding>();
        entity.ToTable("NoticeEmbeddings");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Model).HasMaxLength(200);

        var vectorProperty = entity.Property(e => e.Vector)
            .HasColumnType($"vector(float64, {NoticeEmbeddingVectorDimensions})")
            .HasConversion(NoticeEmbeddingVectorConverter);

        vectorProperty.Metadata.SetValueComparer(NoticeEmbeddingVectorComparer);
        entity.Property(e => e.Source).HasMaxLength(100);

        entity.HasOne(e => e.Notice)
            .WithMany(n => n.Embeddings)
            .HasForeignKey(e => e.NoticeId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.NoticeId).HasDatabaseName("IX_NoticeEmbeddings_NoticeId");
        entity.HasIndex(e => new { e.NoticeId, e.Model })
            .IsUnique()
            .HasDatabaseName("UX_NoticeEmbeddings_Notice_Model");
    }

    private static void ConfigureImportBatch(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ImportBatch>();
        entity.ToTable("ImportBatches");
        entity.HasKey(b => b.Id);

        entity.Property(b => b.SourceFileName).HasMaxLength(256);
        entity.Property(b => b.Period).HasMaxLength(64);
        entity.Property(b => b.Checksum).HasMaxLength(128);
        entity.Property(b => b.Status).HasMaxLength(64);
    }

    private static void ConfigureNoticeSearchVector(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NoticeSearchVector>();
        entity.ToTable("NoticeSearchVectors");
        entity.HasKey(s => s.Id);

        entity.Property(s => s.AggregatedText).HasColumnType("nvarchar(max)");
        entity.Property(s => s.EmbeddingVector).HasColumnType("varbinary(max)");
    }

    private static void ConfigureRefreshToken(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RefreshToken>();
        entity.ToTable("RefreshTokens");
        entity.HasKey(r => r.Id);

        entity.Property(r => r.Token).HasMaxLength(512).IsRequired();

        entity.HasOne(r => r.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(r => r.Token).IsUnique();
    }

    private static byte[] ConvertVectorToBytes(double[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        var buffer = new byte[vector.Length * sizeof(double)];
        Buffer.BlockCopy(vector, 0, buffer, 0, buffer.Length);
        return buffer;
    }

    private static double[] ConvertBytesToVector(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var vector = new double[bytes.Length / sizeof(double)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }
}
