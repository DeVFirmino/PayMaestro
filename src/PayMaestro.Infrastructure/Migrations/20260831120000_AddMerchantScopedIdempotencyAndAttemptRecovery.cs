using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayMaestro.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(Data.PayMaestroDbContext))]
    [Migration("20260831120000_AddMerchantScopedIdempotencyAndAttemptRecovery")]
    public partial class AddMerchantScopedIdempotencyAndAttemptRecovery : Migration
    {
        /// <summary>
        /// Rows written before payments were scoped per merchant belong to no merchant. They are
        /// attributed to a reserved id rather than to a real one, so that no live merchant
        /// inherits another tenant's history through the new unique index.
        /// </summary>
        private const string LegacyMerchantId = "legacy-unscoped";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payment_IdempotencyKey",
                table: "Payment");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PaymentAttempt",
                type: "TEXT",
                nullable: false,
                defaultValue: "Completed");

            migrationBuilder.AddColumn<string>(
                name: "MerchantId",
                table: "Payment",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: LegacyMerchantId);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodToken",
                table: "Payment",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "Payment",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_MerchantId_IdempotencyKey",
                table: "Payment",
                columns: new[] { "MerchantId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payment_MerchantId_IdempotencyKey",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PaymentAttempt");

            migrationBuilder.DropColumn(
                name: "MerchantId",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "PaymentMethodToken",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "Payment");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_IdempotencyKey",
                table: "Payment",
                column: "IdempotencyKey",
                unique: true);
        }
    }
}
