using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignStudio.Api.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class RecoveredFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryMode",
                table: "Senders",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MaxConnections",
                table: "Senders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "TrackEngagement",
                table: "Campaigns",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Engagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    LinkKey = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Bucket = table.Column<long>(type: "INTEGER", nullable: false),
                    At = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Engagement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Engagement_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ContactId",
                table: "Jobs",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_SenderId_State",
                table: "Campaigns",
                columns: new[] { "SenderId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Engagement_JobId_Kind_LinkKey_Bucket",
                table: "Engagement",
                columns: new[] { "JobId", "Kind", "LinkKey", "Bucket" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Attempts_Jobs_JobId",
                table: "Attempts",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_Campaigns_CampaignId",
                table: "Jobs",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_Contacts_ContactId",
                table: "Jobs",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attempts_Jobs_JobId",
                table: "Attempts");

            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_Campaigns_CampaignId",
                table: "Jobs");

            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_Contacts_ContactId",
                table: "Jobs");

            migrationBuilder.DropTable(
                name: "Engagement");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_ContactId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_SenderId_State",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "DeliveryMode",
                table: "Senders");

            migrationBuilder.DropColumn(
                name: "MaxConnections",
                table: "Senders");

            migrationBuilder.DropColumn(
                name: "TrackEngagement",
                table: "Campaigns");
        }
    }
}
