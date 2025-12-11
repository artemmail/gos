using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zakupki.Fetcher.Data;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NoticeDbContext))]
    [Migration("20251207000000_AddCompanyTable")]
    public partial class AddCompanyTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Notices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Inn = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Region = table.Column<byte>(type: "tinyint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notices_CompanyId",
                table: "Notices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "UX_Companies_Inn",
                table: "Companies",
                column: "Inn",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Notices_Companies_CompanyId",
                table: "Notices",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notices_Companies_CompanyId",
                table: "Notices");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Notices_CompanyId",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Notices");
        }
    }
}
