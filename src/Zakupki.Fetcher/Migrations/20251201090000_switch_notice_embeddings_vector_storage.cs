using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    public partial class SwitchNoticeEmbeddingsVectorStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("TRUNCATE TABLE [NoticeEmbeddings];");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Vector",
                table: "NoticeEmbeddings",
                type: "vector(float64, 768)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("TRUNCATE TABLE [NoticeEmbeddings];");

            migrationBuilder.AlterColumn<string>(
                name: "Vector",
                table: "NoticeEmbeddings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "vector(float64, 768)");
        }
    }
}
