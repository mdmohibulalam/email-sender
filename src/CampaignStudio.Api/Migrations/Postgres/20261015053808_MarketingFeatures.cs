using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignStudio.Api.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class MarketingFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "Jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "NextAttemptAt",
                table: "Jobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentIdsJson",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BlocksJson",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CalendarJson",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "HistoryCampaignId",
                table: "Campaigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoryMode",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Issue",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReplyTo",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReturnPath",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Campaigns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SegmentId",
                table: "Campaigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderEmail",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SenderId",
                table: "Campaigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceHtml",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UtmCampaign",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    RecipientEmail = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<long>(type: "bigint", nullable: false),
                    FinishedAt = table.Column<long>(type: "bigint", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Exclusions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exclusions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Library", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Segments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    RulesJson = table.Column<string>(type: "text", nullable: false),
                    Match = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Segments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Senders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Host = table.Column<string>(type: "text", nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    PasswordCipher = table.Column<string>(type: "text", nullable: false),
                    FromEmail = table.Column<string>(type: "text", nullable: false),
                    FromName = table.Column<string>(type: "text", nullable: false),
                    ReplyTo = table.Column<string>(type: "text", nullable: false),
                    ReturnPath = table.Column<string>(type: "text", nullable: false),
                    DelayMilliseconds = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAt = table.Column<long>(type: "bigint", nullable: true),
                    NextSendAt = table.Column<long>(type: "bigint", nullable: false),
                    TestResult = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Senders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CampaignId_State_NextAttemptAt",
                table: "Jobs",
                columns: new[] { "CampaignId", "State", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_ListName_Suppressed",
                table: "Contacts",
                columns: new[] { "ListName", "Suppressed" });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_State_ScheduledAt",
                table: "Campaigns",
                columns: new[] { "State", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_JobId_Number",
                table: "Attempts",
                columns: new[] { "JobId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsentEvents_ContactId_At",
                table: "ConsentEvents",
                columns: new[] { "ContactId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_Exclusions_Kind_Value",
                table: "Exclusions",
                columns: new[] { "Kind", "Value" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "Attempts");

            migrationBuilder.DropTable(
                name: "ConsentEvents");

            migrationBuilder.DropTable(
                name: "Exclusions");

            migrationBuilder.DropTable(
                name: "Library");

            migrationBuilder.DropTable(
                name: "Segments");

            migrationBuilder.DropTable(
                name: "Senders");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_CampaignId_State_NextAttemptAt",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Contacts_ListName_Suppressed",
                table: "Contacts");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_State_ScheduledAt",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "AttachmentIdsJson",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "BlocksJson",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "CalendarJson",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "HistoryCampaignId",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "HistoryMode",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "Issue",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "ReplyTo",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "ReturnPath",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "SegmentId",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "SenderEmail",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "SenderId",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "SourceHtml",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "UtmCampaign",
                table: "Campaigns");
        }
    }
}
