using Microsoft.EntityFrameworkCore.Migrations;
using Zakupki.Fetcher.Data;

#nullable disable

namespace Zakupki.Fetcher.Migrations;

[DbContext(typeof(NoticeDbContext))]
[Migration("20241112000000_AddUserOkpd2Codes")]
public partial class AddUserOkpd2Codes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApplicationUserOkpd2Codes",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Okpd2CodeId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApplicationUserOkpd2Codes", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApplicationUserOkpd2Codes_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ApplicationUserOkpd2Codes_Okpd2Codes_Okpd2CodeId",
                    column: x => x.Okpd2CodeId,
                    principalTable: "Okpd2Codes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApplicationUserOkpd2Codes_Okpd2CodeId",
            table: "ApplicationUserOkpd2Codes",
            column: "Okpd2CodeId");

        migrationBuilder.CreateIndex(
            name: "UX_ApplicationUserOkpd2Codes_User_Code",
            table: "ApplicationUserOkpd2Codes",
            columns: new[] { "UserId", "Okpd2CodeId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ApplicationUserOkpd2Codes");
    }
}
