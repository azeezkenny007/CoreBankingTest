using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBankingTest.DAL.Migrations
{
    /// <inheritdoc />
    public partial class lack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "AccountId",
                keyValue: new Guid("c3d4e5f6-3456-7890-cde1-345678901cde"),
                column: "DateOpened",
                value: new DateTime(2024, 10, 10, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "CustomerId",
                keyValue: new Guid("a1b2c3d4-1234-5678-9abc-123456789abc"),
                columns: new[] { "Address", "BVN", "DateCreated", "DateOfBirth" },
                values: new object[] { "13, Oshinowo street , abule osho", "20000000009", new DateTime(2024, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1995, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "AccountId",
                keyValue: new Guid("c3d4e5f6-3456-7890-cde1-345678901cde"),
                column: "DateOpened",
                value: new DateTime(2025, 10, 17, 22, 47, 51, 413, DateTimeKind.Utc).AddTicks(137));

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "CustomerId",
                keyValue: new Guid("a1b2c3d4-1234-5678-9abc-123456789abc"),
                columns: new[] { "Address", "BVN", "DateCreated", "DateOfBirth" },
                values: new object[] { "13, oshinowo street , abue osho", "20000000000", new DateTime(2025, 10, 7, 22, 47, 51, 412, DateTimeKind.Utc).AddTicks(9688), new DateTime(1995, 11, 6, 22, 47, 51, 412, DateTimeKind.Utc).AddTicks(9660) });
        }
    }
}
