using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ANU_Admissions.Migrations
{
    /// <inheritdoc />
    public partial class CleanupAndAddUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "StudentProfiles",
                type: "nvarchar(450)",
                nullable: true,  // Changed to nullable to allow cleanup
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "NationalId",
                table: "StudentProfiles",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 12, 15, 2, 590, DateTimeKind.Utc).AddTicks(2332));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 12, 15, 2, 590, DateTimeKind.Utc).AddTicks(2343));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 12, 15, 2, 590, DateTimeKind.Utc).AddTicks(2347));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 12, 15, 2, 590, DateTimeKind.Utc).AddTicks(2349));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 12, 15, 2, 590, DateTimeKind.Utc).AddTicks(2352));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 12, 15, 2, 590, DateTimeKind.Utc).AddTicks(2354));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 12, 15, 2, 590, DateTimeKind.Utc).AddTicks(2357));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 12, 15, 2, 590, DateTimeKind.Utc).AddTicks(2359));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastModified",
                value: new DateTime(2026, 2, 7, 12, 15, 2, 590, DateTimeKind.Utc).AddTicks(5356));

            // CLEANUP: Convert empty strings to NULL
            migrationBuilder.Sql(@"
                UPDATE StudentProfiles
                SET NationalId = NULL
                WHERE NationalId = '';

                UPDATE StudentProfiles
                SET PhoneNumber = NULL
                WHERE PhoneNumber = '';
            ");

            // CLEANUP: Remove duplicate NationalId values (keep most recent)
            migrationBuilder.Sql(@"
                WITH DuplicateNationalIds AS (
                    SELECT 
                        Id,
                        NationalId,
                        ROW_NUMBER() OVER (PARTITION BY NationalId ORDER BY ApplicationDate DESC, Id DESC) AS RowNum
                    FROM StudentProfiles
                    WHERE NationalId IS NOT NULL
                )
                UPDATE StudentProfiles
                SET NationalId = NULL
                WHERE Id IN (
                    SELECT Id FROM DuplicateNationalIds WHERE RowNum > 1
                );
            ");

            // CLEANUP: Remove duplicate PhoneNumber values (keep most recent)
            migrationBuilder.Sql(@"
                WITH DuplicatePhones AS (
                    SELECT 
                        Id,
                        PhoneNumber,
                        ROW_NUMBER() OVER (PARTITION BY PhoneNumber ORDER BY ApplicationDate DESC, Id DESC) AS RowNum
                    FROM StudentProfiles
                    WHERE PhoneNumber IS NOT NULL
                )
                UPDATE StudentProfiles
                SET PhoneNumber = NULL
                WHERE Id IN (
                    SELECT Id FROM DuplicatePhones WHERE RowNum > 1
                );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_NationalId",
                table: "StudentProfiles",
                column: "NationalId",
                unique: true,
                filter: "[NationalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_PhoneNumber",
                table: "StudentProfiles",
                column: "PhoneNumber",
                unique: true,
                filter: "[PhoneNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_NationalId",
                table: "StudentProfiles");

            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_PhoneNumber",
                table: "StudentProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "StudentProfiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "NationalId",
                table: "StudentProfiles",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 11, 44, 38, 419, DateTimeKind.Utc).AddTicks(4840));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 11, 44, 38, 419, DateTimeKind.Utc).AddTicks(4850));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 11, 44, 38, 419, DateTimeKind.Utc).AddTicks(4854));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 11, 44, 38, 419, DateTimeKind.Utc).AddTicks(4858));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 11, 44, 38, 419, DateTimeKind.Utc).AddTicks(4860));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 11, 44, 38, 419, DateTimeKind.Utc).AddTicks(4862));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 11, 44, 38, 419, DateTimeKind.Utc).AddTicks(4865));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 7, 11, 44, 38, 419, DateTimeKind.Utc).AddTicks(4868));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastModified",
                value: new DateTime(2026, 2, 7, 11, 44, 38, 419, DateTimeKind.Utc).AddTicks(8086));
        }
    }
}
