using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PPMS.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveFieldsToPrisoner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveNotes",
                table: "Prisoners",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Prisoners",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Prisoners",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Prisoners",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Prisoners",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Prisoners_IsArchived",
                table: "Prisoners",
                column: "IsArchived");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Prisoners_IsArchived",
                table: "Prisoners");

            migrationBuilder.DropColumn(
                name: "ArchiveNotes",
                table: "Prisoners");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Prisoners");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Prisoners");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Prisoners");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Prisoners");
        }
    }
}
