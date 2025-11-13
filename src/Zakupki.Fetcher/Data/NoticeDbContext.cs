using Microsoft.EntityFrameworkCore;
using Zakupki.Fetcher.Data.Entities;

namespace Zakupki.Fetcher.Data;

public class NoticeDbContext : DbContext
{
    public NoticeDbContext(DbContextOptions<NoticeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Notice> Notices => Set<Notice>();

    public DbSet<NoticeVersion> NoticeVersions => Set<NoticeVersion>();

    public DbSet<ProcedureWindow> ProcedureWindows => Set<ProcedureWindow>();

    public DbSet<NoticeAttachment> NoticeAttachments => Set<NoticeAttachment>();

    public DbSet<AttachmentSignature> AttachmentSignatures => Set<AttachmentSignature>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<NoticeSearchVector> NoticeSearchVectors => Set<NoticeSearchVector>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureNotice(modelBuilder);
        ConfigureNoticeVersion(modelBuilder);
        ConfigureProcedureWindow(modelBuilder);
        ConfigureNoticeAttachment(modelBuilder);
        ConfigureAttachmentSignature(modelBuilder);
        ConfigureImportBatch(modelBuilder);
        ConfigureNoticeSearchVector(modelBuilder);
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

        entity.HasMany(n => n.Versions)
            .WithOne(v => v.Notice)
            .HasForeignKey(v => v.NoticeId)
            .OnDelete(DeleteBehavior.Cascade);
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
}
