using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    public partial class AddNoticeEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NoticeEmbeddings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Dimensions = table.Column<int>(type: "int", nullable: false),
                    Vector = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeEmbeddings_Notices_NoticeId",
                        column: x => x.NoticeId,
                        principalTable: "Notices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NoticeEmbeddings_NoticeId",
                table: "NoticeEmbeddings",
                column: "NoticeId");

            migrationBuilder.CreateIndex(
                name: "UX_NoticeEmbeddings_Notice_Model",
                table: "NoticeEmbeddings",
                columns: new[] { "NoticeId", "Model" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoticeEmbeddings");
        }
    }
}
