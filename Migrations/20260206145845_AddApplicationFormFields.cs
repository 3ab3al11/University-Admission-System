using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ANU_Admissions.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationFormFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApplicationDate",
                table: "StudentProfiles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CertificateType",
                table: "StudentProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                table: "StudentProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeatNumber",
                table: "StudentProfiles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationDate",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "CertificateType",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "SeatNumber",
                table: "StudentProfiles");
        }
    }
}
