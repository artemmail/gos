using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Zakupki.Fetcher.Data;

#nullable disable

namespace Zakupki.Fetcher.Migrations;

[DbContext(typeof(NoticeDbContext))]
[Migration("20240910000000_RemoveNoticeEmbeddingMetadata")]
public partial class RemoveNoticeEmbeddingMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_NoticeEmbeddings_Notice_Model",
            table: "NoticeEmbeddings");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "NoticeEmbeddings");

        migrationBuilder.DropColumn(
            name: "Dimensions",
            table: "NoticeEmbeddings");

        migrationBuilder.DropColumn(
            name: "Model",
            table: "NoticeEmbeddings");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            table: "NoticeEmbeddings");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "NoticeEmbeddings",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<int>(
            name: "Dimensions",
            table: "NoticeEmbeddings",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "Model",
            table: "NoticeEmbeddings",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "NoticeEmbeddings",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.CreateIndex(
            name: "UX_NoticeEmbeddings_Notice_Model",
            table: "NoticeEmbeddings",
            columns: new[] { "NoticeId", "Model" },
            unique: true);
    }
}
