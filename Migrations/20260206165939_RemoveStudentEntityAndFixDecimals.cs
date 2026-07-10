using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ANU_Admissions.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStudentEntityAndFixDecimals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Percentage",
                table: "StudentProfiles",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "EquivalentPercentage",
                table: "StudentProfiles",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");

            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "StudentProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentProfileId1",
                table: "Preferences",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "MinimumScore",
                table: "Colleges",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "FinalCutoff",
                table: "Colleges",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentProfileId1",
                table: "Allocations",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 59, 38, 867, DateTimeKind.Utc).AddTicks(7982));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 59, 38, 867, DateTimeKind.Utc).AddTicks(7999));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 59, 38, 867, DateTimeKind.Utc).AddTicks(8003));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 59, 38, 867, DateTimeKind.Utc).AddTicks(8006));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 59, 38, 867, DateTimeKind.Utc).AddTicks(8009));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 59, 38, 867, DateTimeKind.Utc).AddTicks(8012));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 59, 38, 867, DateTimeKind.Utc).AddTicks(8015));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 59, 38, 867, DateTimeKind.Utc).AddTicks(8018));

            migrationBuilder.CreateIndex(
                name: "IX_Preferences_StudentProfileId1",
                table: "Preferences",
                column: "StudentProfileId1");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_StudentProfileId1",
                table: "Allocations",
                column: "StudentProfileId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Allocations_StudentProfiles_StudentProfileId1",
                table: "Allocations",
                column: "StudentProfileId1",
                principalTable: "StudentProfiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Preferences_StudentProfiles_StudentProfileId1",
                table: "Preferences",
                column: "StudentProfileId1",
                principalTable: "StudentProfiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Allocations_StudentProfiles_StudentProfileId1",
                table: "Allocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Preferences_StudentProfiles_StudentProfileId1",
                table: "Preferences");

            migrationBuilder.DropIndex(
                name: "IX_Preferences_StudentProfileId1",
                table: "Preferences");

            migrationBuilder.DropIndex(
                name: "IX_Allocations_StudentProfileId1",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "StudentProfileId1",
                table: "Preferences");

            migrationBuilder.DropColumn(
                name: "StudentProfileId1",
                table: "Allocations");

            migrationBuilder.AlterColumn<decimal>(
                name: "Percentage",
                table: "StudentProfiles",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "EquivalentPercentage",
                table: "StudentProfiles",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "MinimumScore",
                table: "Colleges",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "FinalCutoff",
                table: "Colleges",
                type: "decimal(5,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 42, 33, 499, DateTimeKind.Utc).AddTicks(9952));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 42, 33, 499, DateTimeKind.Utc).AddTicks(9967));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 42, 33, 499, DateTimeKind.Utc).AddTicks(9970));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 42, 33, 499, DateTimeKind.Utc).AddTicks(9978));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 42, 33, 499, DateTimeKind.Utc).AddTicks(9981));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 42, 33, 499, DateTimeKind.Utc).AddTicks(9983));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 42, 33, 499, DateTimeKind.Utc).AddTicks(9986));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 42, 33, 499, DateTimeKind.Utc).AddTicks(9989));
        }
    }
}
