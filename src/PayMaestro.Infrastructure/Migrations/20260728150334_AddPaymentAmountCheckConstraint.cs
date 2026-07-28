using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayMaestro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAmountCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FraudFlags_Payment_PaymentId",
                table: "FraudFlags");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAttempt_Payment_PaymentId",
                table: "PaymentAttempt");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payment_Amount_Positive",
                table: "Payment",
                sql: "CAST(Amount AS REAL) > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_FraudFlags_Payment_PaymentId",
                table: "FraudFlags",
                column: "PaymentId",
                principalTable: "Payment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAttempt_Payment_PaymentId",
                table: "PaymentAttempt",
                column: "PaymentId",
                principalTable: "Payment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FraudFlags_Payment_PaymentId",
                table: "FraudFlags");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAttempt_Payment_PaymentId",
                table: "PaymentAttempt");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payment_Amount_Positive",
                table: "Payment");

            migrationBuilder.AddForeignKey(
                name: "FK_FraudFlags_Payment_PaymentId",
                table: "FraudFlags",
                column: "PaymentId",
                principalTable: "Payment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAttempt_Payment_PaymentId",
                table: "PaymentAttempt",
                column: "PaymentId",
                principalTable: "Payment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
