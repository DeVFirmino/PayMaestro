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
        /// Rows written before payments were scoped per merchant belong to no merchant, and no
        /// backfill can attribute them truthfully. Keeping them under a pseudo-tenant would
        /// break their idempotency contract anyway: the merchant that created them could never
        /// reach them again. This migration deletes them instead — explicitly, in the migration
        /// itself, so applying it to a database with history is a visible decision, not an
        /// accident. It is therefore irreversible.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM FraudFlags;");
            migrationBuilder.Sql("DELETE FROM PaymentAttempt;");
            migrationBuilder.Sql("DELETE FROM Payment;");

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
                defaultValue: "");

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

        /// <summary>
        /// The Up deleted the pre-scoping rows, and once two merchants have used the same key
        /// the global unique index cannot be rebuilt either. Rolling back means restoring the
        /// database from a backup, not running this migration in reverse.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new NotSupportedException(
                "This migration deleted the pre-merchant-scoping rows and cannot be reverted. Restore from a backup.");
    }
}
