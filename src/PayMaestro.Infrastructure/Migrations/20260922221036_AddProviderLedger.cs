using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayMaestro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderLedger",
                columns: table => new
                {
                    ProviderIdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ResultType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ResponseCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SettledAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderLedger", x => x.ProviderIdempotencyKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderLedger");
        }
    }
}
