using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    public partial class AddNoticeClassifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KvrCode",
                table: "Notices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KvrName",
                table: "Notices",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Okpd2Code",
                table: "Notices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Okpd2Name",
                table: "Notices",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KvrCode",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "KvrName",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "Okpd2Code",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "Okpd2Name",
                table: "Notices");
        }
    }
}
