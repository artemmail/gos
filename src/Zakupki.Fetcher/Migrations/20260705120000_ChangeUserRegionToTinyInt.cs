using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    public partial class ChangeUserRegionToTinyInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "RegionNumeric",
                table: "ApplicationUserRegions",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.Sql(
                @"UPDATE ApplicationUserRegions
SET RegionNumeric = TRY_CONVERT(tinyint, Region)
WHERE TRY_CONVERT(tinyint, Region) IS NOT NULL;");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationUserRegions_UserId_Region",
                table: "ApplicationUserRegions");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "ApplicationUserRegions");

            migrationBuilder.RenameColumn(
                name: "RegionNumeric",
                table: "ApplicationUserRegions",
                newName: "Region");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUserRegions_UserId_Region",
                table: "ApplicationUserRegions",
                columns: new[] { "UserId", "Region" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationUserRegions_UserId_Region",
                table: "ApplicationUserRegions");

            migrationBuilder.AddColumn<string>(
                name: "RegionText",
                table: "ApplicationUserRegions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                @"UPDATE ApplicationUserRegions
SET RegionText = RIGHT('0' + CONVERT(varchar(3), Region), 2)
WHERE Region IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "ApplicationUserRegions");

            migrationBuilder.RenameColumn(
                name: "RegionText",
                table: "ApplicationUserRegions",
                newName: "Region");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUserRegions_UserId_Region",
                table: "ApplicationUserRegions",
                columns: new[] { "UserId", "Region" },
                unique: true);
        }
    }
}
