using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zakupki.Fetcher.Data;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NoticeDbContext))]
    [Migration("20251221000000_AddPostalIndices")]
    public partial class AddPostalIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PostalIndices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Index = table.Column<int>(type: "int", nullable: false),
                    OPSName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OPSType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    OPSSubm = table.Column<int>(type: "int", nullable: true),
                    Region = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegionId = table.Column<byte>(type: "tinyint", nullable: true),
                    Autonom = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Area = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    City = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    City1 = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ActDate = table.Column<DateTime>(type: "date", nullable: true),
                    IndexOld = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostalIndices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostalIndices_Index",
                table: "PostalIndices",
                column: "Index",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostalIndices");
        }
    }
}
