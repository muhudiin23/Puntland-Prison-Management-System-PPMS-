using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PPMS.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaseReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CaseTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CrimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CrimeDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrisonerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PrisonId = table.Column<int>(type: "int", nullable: true),
                    DateOfCrime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateReported = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InvestigationStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CaseStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OfficerInCharge = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DocumentPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvidencePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseReports_Prisons_PrisonId",
                        column: x => x.PrisonId,
                        principalTable: "Prisons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseReports_CaseId",
                table: "CaseReports",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseReports_CaseStatus",
                table: "CaseReports",
                column: "CaseStatus");

            migrationBuilder.CreateIndex(
                name: "IX_CaseReports_CrimeType",
                table: "CaseReports",
                column: "CrimeType");

            migrationBuilder.CreateIndex(
                name: "IX_CaseReports_DateOfCrime",
                table: "CaseReports",
                column: "DateOfCrime");

            migrationBuilder.CreateIndex(
                name: "IX_CaseReports_PrisonId",
                table: "CaseReports",
                column: "PrisonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseReports");
        }
    }
}
