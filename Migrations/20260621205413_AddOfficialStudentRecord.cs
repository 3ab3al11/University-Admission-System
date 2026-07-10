using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ANU_Admissions.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficialStudentRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PhoneNumber has a unique filtered index from an earlier migration.
            // SQL Server forbids ALTER COLUMN while an index depends on it, so
            // we drop the index, relax the column to NULL, then recreate it.
            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_PhoneNumber",
                table: "StudentProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "StudentProfiles",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_PhoneNumber",
                table: "StudentProfiles",
                column: "PhoneNumber",
                unique: true,
                filter: "[PhoneNumber] IS NOT NULL");

            migrationBuilder.AddColumn<int>(
                name: "OfficialRecordId",
                table: "StudentProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OfficialStudentRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeatNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalScore = table.Column<decimal>(type: "decimal(7,3)", precision: 7, scale: 3, nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(7,3)", precision: 7, scale: 3, nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    EquivalentPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    StatusDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsEligible = table.Column<bool>(type: "bit", nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportBatch = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialStudentRecords", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 21, 20, 54, 10, 817, DateTimeKind.Utc).AddTicks(2949));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 21, 20, 54, 10, 817, DateTimeKind.Utc).AddTicks(2963));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 21, 20, 54, 10, 817, DateTimeKind.Utc).AddTicks(2966));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 21, 20, 54, 10, 817, DateTimeKind.Utc).AddTicks(2968));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 21, 20, 54, 10, 817, DateTimeKind.Utc).AddTicks(2970));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 21, 20, 54, 10, 817, DateTimeKind.Utc).AddTicks(2972));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 21, 20, 54, 10, 817, DateTimeKind.Utc).AddTicks(2974));

            migrationBuilder.UpdateData(
                table: "Colleges",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 21, 20, 54, 10, 817, DateTimeKind.Utc).AddTicks(2976));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastModified",
                value: new DateTime(2026, 6, 21, 20, 54, 10, 817, DateTimeKind.Utc).AddTicks(9566));

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_OfficialRecordId",
                table: "StudentProfiles",
                column: "OfficialRecordId",
                unique: true,
                filter: "[OfficialRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OfficialStudentRecords_NationalId",
                table: "OfficialStudentRecords",
                column: "NationalId",
                unique: true,
                filter: "[NationalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OfficialStudentRecords_SeatNumber",
                table: "OfficialStudentRecords",
                column: "SeatNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentProfiles_OfficialStudentRecords_OfficialRecordId",
                table: "StudentProfiles",
                column: "OfficialRecordId",
                principalTable: "OfficialStudentRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentProfiles_OfficialStudentRecords_OfficialRecordId",
                table: "StudentProfiles");

            migrationBuilder.DropTable(
                name: "OfficialStudentRecords");

            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_OfficialRecordId",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "OfficialRecordId",
                table: "StudentProfiles");

            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_PhoneNumber",
                table: "StudentProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "StudentProfiles",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_PhoneNumber",
                table: "StudentProfiles",
                column: "PhoneNumber",
                unique: true,
                filter: "[PhoneNumber] IS NOT NULL");

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
        }
    }
}
