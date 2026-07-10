using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ANU_Admissions.Migrations
{
    /// <inheritdoc />
    public partial class FixDocumentStudentProfileFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Students_StudentId",
                table: "Documents");

            migrationBuilder.RenameColumn(
                name: "StudentId",
                table: "Documents",
                newName: "StudentProfileId");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_StudentId",
                table: "Documents",
                newName: "IX_Documents_StudentProfileId");

            migrationBuilder.AddColumn<int>(
                name: "StudentProfileId1",
                table: "Preferences",
                type: "int",
                nullable: true);

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
                value: new DateTime(2026, 2, 6, 16, 33, 58, 797, DateTimeKind.Utc).AddTicks(4854));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 33, 58, 797, DateTimeKind.Utc).AddTicks(4870));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 33, 58, 797, DateTimeKind.Utc).AddTicks(4873));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 33, 58, 797, DateTimeKind.Utc).AddTicks(4876));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 33, 58, 797, DateTimeKind.Utc).AddTicks(4943));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 33, 58, 797, DateTimeKind.Utc).AddTicks(4946));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 33, 58, 797, DateTimeKind.Utc).AddTicks(4948));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 33, 58, 797, DateTimeKind.Utc).AddTicks(4951));

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
                name: "FK_Documents_StudentProfiles_StudentProfileId",
                table: "Documents",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_Documents_StudentProfiles_StudentProfileId",
                table: "Documents");

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
                name: "StudentProfileId1",
                table: "Preferences");

            migrationBuilder.DropColumn(
                name: "StudentProfileId1",
                table: "Allocations");

            migrationBuilder.RenameColumn(
                name: "StudentProfileId",
                table: "Documents",
                newName: "StudentId");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_StudentProfileId",
                table: "Documents",
                newName: "IX_Documents_StudentId");

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 9, 50, 314, DateTimeKind.Utc).AddTicks(5584));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 9, 50, 314, DateTimeKind.Utc).AddTicks(5595));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 9, 50, 314, DateTimeKind.Utc).AddTicks(5599));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 9, 50, 314, DateTimeKind.Utc).AddTicks(5602));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 9, 50, 314, DateTimeKind.Utc).AddTicks(5604));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 9, 50, 314, DateTimeKind.Utc).AddTicks(5607));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 9, 50, 314, DateTimeKind.Utc).AddTicks(5610));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 16, 9, 50, 314, DateTimeKind.Utc).AddTicks(5613));

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Students_StudentId",
                table: "Documents",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
