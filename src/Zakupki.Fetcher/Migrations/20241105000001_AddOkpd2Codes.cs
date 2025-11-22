using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Zakupki.Fetcher.Data;

#nullable disable

namespace Zakupki.Fetcher.Migrations;

[DbContext(typeof(NoticeDbContext))]
[Migration("20241105000001_AddOkpd2Codes")]
public partial class AddOkpd2Codes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Okpd2Codes",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Okpd2Codes", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "UX_Okpd2Codes_Code",
            table: "Okpd2Codes",
            column: "Code",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Okpd2Codes");
    }
}
