using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PPMS.Migrations
{
    /// <inheritdoc />
    public partial class AddFormerPrisonersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormerPrisoners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrisonerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CrimeType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SentenceDurationMonths = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriminalStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EmergencyContact = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FingerprintData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalPrisonId = table.Column<int>(type: "int", nullable: true),
                    PrisonName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PrisonCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OriginalCreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OriginalUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ArchivedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ArchiveNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EvidenceCount = table.Column<int>(type: "int", nullable: false),
                    RecordMovedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormerPrisoners", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormerPrisoners");
        }
    }
}
