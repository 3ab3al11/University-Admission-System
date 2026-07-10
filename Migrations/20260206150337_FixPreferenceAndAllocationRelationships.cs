using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ANU_Admissions.Migrations
{
    /// <inheritdoc />
    public partial class FixPreferenceAndAllocationRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Allocations_Students_StudentId",
                table: "Allocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Preferences_Students_StudentId",
                table: "Preferences");

            migrationBuilder.DropIndex(
                name: "IX_Students_Email",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_NationalId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Colleges_Code",
                table: "Colleges");

            migrationBuilder.DropIndex(
                name: "IX_Allocations_StudentId",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Allocations");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "Students",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "StudentId",
                table: "Preferences",
                newName: "StudentProfileId");

            migrationBuilder.RenameColumn(
                name: "Priority",
                table: "Preferences",
                newName: "Rank");

            migrationBuilder.RenameIndex(
                name: "IX_Preferences_StudentId_Priority",
                table: "Preferences",
                newName: "IX_Preferences_StudentProfileId_Rank");

            migrationBuilder.RenameColumn(
                name: "StudentId",
                table: "Allocations",
                newName: "StudentProfileId");

            migrationBuilder.AlterColumn<string>(
                name: "NationalId",
                table: "Students",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Students",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLogin",
                table: "Students",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "Students",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Students",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Colleges",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<DateTime>(
                name: "DocumentsSubmissionDate",
                table: "Allocations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DocumentsSubmitted",
                table: "Allocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "Colleges",
                columns: new[] { "Id", "AllowedSections", "Capacity", "Code", "CreatedAt", "Description", "FinalCutoff", "IsActive", "MinimumScore", "NameAr", "NameEn" },
                values: new object[,]
                {
                    { 1, "علمي علوم", 200, "MED", new DateTime(2026, 2, 6, 15, 3, 36, 658, DateTimeKind.Utc).AddTicks(4818), "", null, true, 95.0m, "كلية الطب", "Faculty of Medicine" },
                    { 2, "علمي علوم", 150, "PHARM", new DateTime(2026, 2, 6, 15, 3, 36, 658, DateTimeKind.Utc).AddTicks(4831), "", null, true, 93.0m, "كلية الصيدلة", "Faculty of Pharmacy" },
                    { 3, "علمي علوم", 120, "DENT", new DateTime(2026, 2, 6, 15, 3, 36, 658, DateTimeKind.Utc).AddTicks(4834), "", null, true, 94.0m, "كلية طب الأسنان", "Faculty of Dentistry" },
                    { 4, "علمي رياضة", 300, "ENG", new DateTime(2026, 2, 6, 15, 3, 36, 658, DateTimeKind.Utc).AddTicks(4837), "", null, true, 90.0m, "كلية الهندسة", "Faculty of Engineering" },
                    { 5, "علمي رياضة,علمي علوم", 250, "CS", new DateTime(2026, 2, 6, 15, 3, 36, 658, DateTimeKind.Utc).AddTicks(4840), "", null, true, 88.0m, "كلية الحاسبات والذكاء الاصطناعي", "Faculty of Computer Science and AI" },
                    { 6, "علمي رياضة,علمي علوم,أدبي", 400, "BUS", new DateTime(2026, 2, 6, 15, 3, 36, 658, DateTimeKind.Utc).AddTicks(4842), "", null, true, 75.0m, "كلية إدارة الأعمال", "Faculty of Business Administration" },
                    { 7, "علمي رياضة,علمي علوم,أدبي", 300, "ALSUN", new DateTime(2026, 2, 6, 15, 3, 36, 658, DateTimeKind.Utc).AddTicks(4845), "", null, true, 70.0m, "كلية الألسن", "Faculty of Languages" },
                    { 8, "علمي رياضة,علمي علوم,أدبي", 350, "FIN", new DateTime(2026, 2, 6, 15, 3, 36, 658, DateTimeKind.Utc).AddTicks(4848), "", null, true, 73.0m, "كلية المالية والإدارة", "Faculty of Finance and Administration" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_StudentProfileId",
                table: "Allocations",
                column: "StudentProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Allocations_StudentProfiles_StudentProfileId",
                table: "Allocations",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Preferences_StudentProfiles_StudentProfileId",
                table: "Preferences",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Allocations_StudentProfiles_StudentProfileId",
                table: "Allocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Preferences_StudentProfiles_StudentProfileId",
                table: "Preferences");

            migrationBuilder.DropIndex(
                name: "IX_Allocations_StudentProfileId",
                table: "Allocations");

            migrationBuilder.DeleteData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DropColumn(
                name: "LastLogin",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "DocumentsSubmissionDate",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "DocumentsSubmitted",
                table: "Allocations");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "Students",
                newName: "PasswordHash");

            migrationBuilder.RenameColumn(
                name: "StudentProfileId",
                table: "Preferences",
                newName: "StudentId");

            migrationBuilder.RenameColumn(
                name: "Rank",
                table: "Preferences",
                newName: "Priority");

            migrationBuilder.RenameIndex(
                name: "IX_Preferences_StudentProfileId_Rank",
                table: "Preferences",
                newName: "IX_Preferences_StudentId_Priority");

            migrationBuilder.RenameColumn(
                name: "StudentProfileId",
                table: "Allocations",
                newName: "StudentId");

            migrationBuilder.AlterColumn<string>(
                name: "NationalId",
                table: "Students",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Students",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Colleges",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Allocations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_Email",
                table: "Students",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_NationalId",
                table: "Students",
                column: "NationalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Colleges_Code",
                table: "Colleges",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_StudentId",
                table: "Allocations",
                column: "StudentId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Allocations_Students_StudentId",
                table: "Allocations",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Preferences_Students_StudentId",
                table: "Preferences",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
