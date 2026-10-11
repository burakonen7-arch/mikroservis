using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stock.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OUTBOX",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Topic = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "CLOB", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    TraceParent = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OUTBOX", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "STOCKS",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Quantity = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Version = table.Column<long>(type: "NUMBER(19)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STOCKS", x => x.ProductId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OUTBOX");

            migrationBuilder.DropTable(
                name: "STOCKS");
        }
    }
}
