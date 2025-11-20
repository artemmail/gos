using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    public partial class UpdateUserQueryVectorMapping : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<SqlVector<float>>(
                name: "Vector",
                table: "UserQueryVectors",
                type: "vector(768, float32)",
                nullable: true,
                oldClrType: typeof(SqlVector<float>),
                oldType: "vector(768)",
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<SqlVector<float>>(
                name: "Vector",
                table: "UserQueryVectors",
                type: "vector(768)",
                nullable: true,
                oldClrType: typeof(SqlVector<float>),
                oldType: "vector(768, float32)",
                oldNullable: true);
        }
    }
}
