using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitRoute.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRetryScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_ProcessedOnUtc_OccurredOnUtc",
                table: "outbox_messages");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptUtc",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedOnUtc_NextAttemptUtc_OccurredOnUtc",
                table: "outbox_messages",
                columns: new[] { "ProcessedOnUtc", "NextAttemptUtc", "OccurredOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_ProcessedOnUtc_NextAttemptUtc_OccurredOnUtc",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "NextAttemptUtc",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedOnUtc_OccurredOnUtc",
                table: "outbox_messages",
                columns: new[] { "ProcessedOnUtc", "OccurredOnUtc" });
        }
    }
}
