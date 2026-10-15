using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignStudio.Api.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InboxProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inboxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordCipher = table.Column<string>(type: "TEXT", nullable: false),
                    SignupFormId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AutoPoll = table.Column<bool>(type: "INTEGER", nullable: false),
                    VerifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    NextPollAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inboxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InboxId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Uid = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    At = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxReceipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inboxes_AutoPoll_NextPollAt",
                table: "Inboxes",
                columns: new[] { "AutoPoll", "NextPollAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxReceipts_InboxId_Uid",
                table: "InboxReceipts",
                columns: new[] { "InboxId", "Uid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inboxes");

            migrationBuilder.DropTable(
                name: "InboxReceipts");
        }
    }
}
