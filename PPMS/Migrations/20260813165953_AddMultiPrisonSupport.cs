using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PPMS.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiPrisonSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedPrisonId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrisonId",
                table: "Activities",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PrisonerTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrisonerId = table.Column<int>(type: "int", nullable: false),
                    SourcePrisonId = table.Column<int>(type: "int", nullable: false),
                    DestinationPrisonId = table.Column<int>(type: "int", nullable: false),
                    TransferReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TransferNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AuthorizedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrisonerTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrisonerTransfers_Prisoners_PrisonerId",
                        column: x => x.PrisonerId,
                        principalTable: "Prisoners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrisonerTransfers_Prisons_DestinationPrisonId",
                        column: x => x.DestinationPrisonId,
                        principalTable: "Prisons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrisonerTransfers_Prisons_SourcePrisonId",
                        column: x => x.SourcePrisonId,
                        principalTable: "Prisons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AssignedPrisonId",
                table: "AspNetUsers",
                column: "AssignedPrisonId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_PrisonId",
                table: "Alerts",
                column: "PrisonId");

            migrationBuilder.CreateIndex(
                name: "IX_PrisonerTransfers_DestinationPrisonId",
                table: "PrisonerTransfers",
                column: "DestinationPrisonId");

            migrationBuilder.CreateIndex(
                name: "IX_PrisonerTransfers_PrisonerId",
                table: "PrisonerTransfers",
                column: "PrisonerId");

            migrationBuilder.CreateIndex(
                name: "IX_PrisonerTransfers_SourcePrisonId",
                table: "PrisonerTransfers",
                column: "SourcePrisonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_Prisons_PrisonId",
                table: "Alerts",
                column: "PrisonId",
                principalTable: "Prisons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Prisons_AssignedPrisonId",
                table: "AspNetUsers",
                column: "AssignedPrisonId",
                principalTable: "Prisons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_Prisons_PrisonId",
                table: "Alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Prisons_AssignedPrisonId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "PrisonerTransfers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AssignedPrisonId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_PrisonId",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "AssignedPrisonId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PrisonId",
                table: "Activities");
        }
    }
}
