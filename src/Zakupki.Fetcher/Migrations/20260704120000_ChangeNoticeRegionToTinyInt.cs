using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    public partial class ChangeNoticeRegionToTinyInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte?>(
                name: "RegionNumeric",
                table: "Notices",
                type: "tinyint",
                nullable: true);

            migrationBuilder.Sql(
                @"UPDATE Notices
SET RegionNumeric = TRY_CONVERT(tinyint, Region)
WHERE TRY_CONVERT(tinyint, Region) IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Notices");

            migrationBuilder.RenameColumn(
                name: "RegionNumeric",
                table: "Notices",
                newName: "Region");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegionText",
                table: "Notices",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql(
                @"UPDATE Notices
SET RegionText = RIGHT('0' + CONVERT(varchar(3), Region), 2)
WHERE Region IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Notices");

            migrationBuilder.RenameColumn(
                name: "RegionText",
                table: "Notices",
                newName: "Region");
        }
    }
}
