using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zakupki.Fetcher.Data;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NoticeDbContext))]
    [Migration("20251215000000_AddNoticeSourceAndFederalLaw")]
    public partial class AddNoticeSourceAndFederalLaw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "Source",
                table: "Notices",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<int>(
                name: "FederalLaw",
                table: "Notices",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "FederalLaw",
                table: "Notices");
        }
    }
}
