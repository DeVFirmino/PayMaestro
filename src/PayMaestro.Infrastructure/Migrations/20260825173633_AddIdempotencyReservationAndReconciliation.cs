using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayMaestro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyReservationAndReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderIdempotencyKey",
                table: "PaymentAttempt",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                table: "Payment",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderIdempotencyKey",
                table: "PaymentAttempt");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "Payment");
        }
    }
}
