using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    public partial class AddJsonAndPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RawXml",
                table: "NoticeVersions");

            migrationBuilder.DropColumn(
                name: "RawXml",
                table: "Notices");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxPrice",
                table: "Notices",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaxPriceCurrencyCode",
                table: "Notices",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaxPriceCurrencyName",
                table: "Notices",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawJson",
                table: "NoticeVersions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawJson",
                table: "Notices",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxPrice",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "MaxPriceCurrencyCode",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "MaxPriceCurrencyName",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "RawJson",
                table: "NoticeVersions");

            migrationBuilder.DropColumn(
                name: "RawJson",
                table: "Notices");

            migrationBuilder.AddColumn<byte[]>(
                name: "RawXml",
                table: "NoticeVersions",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RawXml",
                table: "Notices",
                type: "varbinary(max)",
                nullable: true);
        }
    }
}
