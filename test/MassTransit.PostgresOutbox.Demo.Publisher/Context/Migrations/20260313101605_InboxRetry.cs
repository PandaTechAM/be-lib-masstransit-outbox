using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MassTransit.PostgresOutbox.Demo.Context.Migrations
{
    /// <inheritdoc />
    public partial class InboxRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DestinationAddress",
                table: "InboxMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "InboxMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "InboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Payload",
                table: "InboxMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "InboxMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "InboxMessages",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_State_RetryCount_NextRetryAt",
                table: "InboxMessages",
                columns: new[] { "State", "RetryCount", "NextRetryAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InboxMessages_State_RetryCount_NextRetryAt",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "DestinationAddress",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "Payload",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "InboxMessages");
        }
    }
}
