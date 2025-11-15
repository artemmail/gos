using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    public partial class AddNoticeCollectingEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CollectingEnd",
                table: "Notices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notices_CollectingEnd",
                table: "Notices",
                column: "CollectingEnd");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notices_CollectingEnd",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "CollectingEnd",
                table: "Notices");
        }
    }
}
