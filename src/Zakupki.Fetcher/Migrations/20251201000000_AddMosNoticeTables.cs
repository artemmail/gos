using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Zakupki.Fetcher.Data;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NoticeDbContext))]
    [Migration("20251201000000_AddMosNoticeTables")]
    public partial class AddMosNoticeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MosNotices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<int>(type: "int", nullable: false),
                    RegisterNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RegistrationDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SummingUpDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndFillingDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PlanDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InitialSum = table.Column<double>(type: "float", nullable: true),
                    StateId = table.Column<int>(type: "int", nullable: true),
                    StateName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FederalLawName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CustomerInn = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InsertedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MosNotices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MosNoticeAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MosNoticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedContentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DocumentKindCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DocumentKindName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BinaryContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    MarkdownContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InsertedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MosNoticeAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MosNoticeAttachments_MosNotices_MosNoticeId",
                        column: x => x.MosNoticeId,
                        principalTable: "MosNotices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MosNoticeAttachments_FileName",
                table: "MosNoticeAttachments",
                column: "FileName");

            migrationBuilder.CreateIndex(
                name: "UX_MosNoticeAttachments_ContentId_Notice",
                table: "MosNoticeAttachments",
                columns: new[] { "PublishedContentId", "MosNoticeId" },
                unique: true,
                filter: "[PublishedContentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MosNoticeAttachments_MosNoticeId",
                table: "MosNoticeAttachments",
                column: "MosNoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_MosNotices_RegistrationDate",
                table: "MosNotices",
                column: "RegistrationDate");

            migrationBuilder.CreateIndex(
                name: "UX_MosNotices_RegisterNumber",
                table: "MosNotices",
                column: "RegisterNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MosNoticeAttachments");

            migrationBuilder.DropTable(
                name: "MosNotices");
        }
    }
}
