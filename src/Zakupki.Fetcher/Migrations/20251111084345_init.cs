using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Period = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Checksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Period = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EntryName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: true),
                    SchemeVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PurchaseNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PublishDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Href = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PlacingWayCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PlacingWayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EtpCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    EtpName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EtpUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ContractConclusionOnSt83Ch2 = table.Column<bool>(type: "bit", nullable: true),
                    PurchaseObjectInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Article15FeaturesInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawXml = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NoticeVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    VersionReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RawXml = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Hash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InsertedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeVersions_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NoticeVersions_Notices_NoticeId",
                        column: x => x.NoticeId,
                        principalTable: "Notices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoticeAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedContentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DocumentKindCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DocumentKindName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BinaryContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    InsertedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeAttachments_NoticeVersions_NoticeVersionId",
                        column: x => x.NoticeVersionId,
                        principalTable: "NoticeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoticeSearchVectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregatedText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmbeddingVector = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeSearchVectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeSearchVectors_NoticeVersions_NoticeVersionId",
                        column: x => x.NoticeVersionId,
                        principalTable: "NoticeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcedureWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectingStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CollectingEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BiddingDateRaw = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SummarizingDateRaw = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FirstPartsDateRaw = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SubmissionProcedureDateRaw = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SecondPartsDateRaw = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcedureWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcedureWindows_NoticeVersions_NoticeVersionId",
                        column: x => x.NoticeVersionId,
                        principalTable: "NoticeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttachmentSignatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignatureType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SignatureValue = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttachmentSignatures_NoticeAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "NoticeAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentSignatures_AttachmentId",
                table: "AttachmentSignatures",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeAttachments_DocKindCode",
                table: "NoticeAttachments",
                column: "DocumentKindCode");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeAttachments_FileName",
                table: "NoticeAttachments",
                column: "FileName");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeAttachments_NoticeVersionId",
                table: "NoticeAttachments",
                column: "NoticeVersionId");

            migrationBuilder.CreateIndex(
                name: "UX_NoticeAttachments_ContentId_Version",
                table: "NoticeAttachments",
                columns: new[] { "PublishedContentId", "NoticeVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notices_Period",
                table: "Notices",
                column: "Period");

            migrationBuilder.CreateIndex(
                name: "IX_Notices_PurchaseNumber",
                table: "Notices",
                column: "PurchaseNumber");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeSearchVectors_NoticeVersionId",
                table: "NoticeSearchVectors",
                column: "NoticeVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoticeVersions_ImportBatchId",
                table: "NoticeVersions",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeVersions_NoticeId",
                table: "NoticeVersions",
                column: "NoticeId");

            migrationBuilder.CreateIndex(
                name: "UX_NoticeVersions_External_Version",
                table: "NoticeVersions",
                columns: new[] { "ExternalId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureWindows_CollectingEnd",
                table: "ProcedureWindows",
                column: "CollectingEnd");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureWindows_CollectingStart",
                table: "ProcedureWindows",
                column: "CollectingStart");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureWindows_NoticeVersionId",
                table: "ProcedureWindows",
                column: "NoticeVersionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttachmentSignatures");

            migrationBuilder.DropTable(
                name: "NoticeSearchVectors");

            migrationBuilder.DropTable(
                name: "ProcedureWindows");

            migrationBuilder.DropTable(
                name: "NoticeAttachments");

            migrationBuilder.DropTable(
                name: "NoticeVersions");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "Notices");
        }
    }
}
