using System;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    public partial class AddNoticeVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoticeEmbeddings");

            migrationBuilder.AddColumn<SqlVector<float>>(
                name: "Vector",
                table: "Notices",
                type: "vector(768)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Vector",
                table: "Notices");

            migrationBuilder.CreateTable(
                name: "NoticeEmbeddings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Vector = table.Column<SqlVector<float>>(type: "vector(768)", nullable: false)
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
        }
    }
}
