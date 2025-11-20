using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    public partial class UpdateUserQueryVectorType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VectorJson",
                table: "UserQueryVectors");

            migrationBuilder.AddColumn<SqlVector<float>>(
                name: "Vector",
                table: "UserQueryVectors",
                type: "vector(768)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Vector",
                table: "UserQueryVectors");

            migrationBuilder.AddColumn<string>(
                name: "VectorJson",
                table: "UserQueryVectors",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
