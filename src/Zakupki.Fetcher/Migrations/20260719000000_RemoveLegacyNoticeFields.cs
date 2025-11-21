using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyNoticeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notices_Period",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "Article15FeaturesInfo",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "ContractConclusionOnSt83Ch2",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "DocumentNumber",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "EntryName",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "EtpCode",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "MaxPriceCurrencyCode",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "MaxPriceCurrencyName",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "Period",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "PlacingWayName",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "SchemeVersion",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "VersionNumber",
                table: "Notices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Article15FeaturesInfo",
                table: "Notices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ContractConclusionOnSt83Ch2",
                table: "Notices",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Notices",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                table: "Notices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "Notices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntryName",
                table: "Notices",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EtpCode",
                table: "Notices",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Notices",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

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
                name: "Period",
                table: "Notices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlacingWayName",
                table: "Notices",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchemeVersion",
                table: "Notices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Notices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Notices",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "VersionNumber",
                table: "Notices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notices_Period",
                table: "Notices",
                column: "Period");
        }
    }
}
