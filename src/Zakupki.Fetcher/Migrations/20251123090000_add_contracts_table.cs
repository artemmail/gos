using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    public partial class AddContractsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntryName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Period = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RegNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Number = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VersionNumber = table.Column<int>(type: "int", nullable: true),
                    SchemeVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PurchaseNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ContractSubject = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CurrencyName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Okpd2Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Okpd2Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Href = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PublishDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Contracts_ExternalId",
                table: "Contracts",
                column: "ExternalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contracts");
        }
    }
}
