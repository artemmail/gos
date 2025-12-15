using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NoticeDbContext))]
    [Migration("20251220000000_AddNoticeUncompletedFlag")]
    public partial class AddNoticeUncompletedFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Uncompleted",
                table: "Notices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                $"UPDATE Notices SET Uncompleted = 1 WHERE Source = {(int)NoticeSource.Mos} AND RawJson IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Uncompleted",
                table: "Notices");
        }
    }
}
