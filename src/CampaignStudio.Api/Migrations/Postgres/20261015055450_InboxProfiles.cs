using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignStudio.Api.Migrations.Postgres
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Host = table.Column<string>(type: "text", nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    PasswordCipher = table.Column<string>(type: "text", nullable: false),
                    SignupFormId = table.Column<Guid>(type: "uuid", nullable: true),
                    AutoPoll = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<long>(type: "bigint", nullable: true),
                    NextPollAt = table.Column<long>(type: "bigint", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inboxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    Uid = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<long>(type: "bigint", nullable: false)
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
