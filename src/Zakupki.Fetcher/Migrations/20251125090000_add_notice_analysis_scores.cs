using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    public partial class AddNoticeAnalysisScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DecisionScore",
                table: "NoticeAnalyses",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Recommended",
                table: "NoticeAnalyses",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecisionScore",
                table: "NoticeAnalyses");

            migrationBuilder.DropColumn(
                name: "Recommended",
                table: "NoticeAnalyses");
        }
    }
}
