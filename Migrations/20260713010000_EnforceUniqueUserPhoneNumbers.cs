using ANU_Admissions.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ANU_Admissions.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260713010000_EnforceUniqueUserPhoneNumbers")]
public partial class EnforceUniqueUserPhoneNumbers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Normalize legacy contact values before reducing the column length.
        // Values outside the application's 32-character storage limit cannot be
        // valid Egyptian mobile numbers, so they are cleared rather than cut.
        migrationBuilder.Sql("""
            UPDATE AspNetUsers
            SET PhoneNumber = LTRIM(RTRIM(PhoneNumber))
            WHERE PhoneNumber IS NOT NULL;

            UPDATE AspNetUsers
            SET ParentPhoneNumber = LTRIM(RTRIM(ParentPhoneNumber))
            WHERE ParentPhoneNumber IS NOT NULL;

            UPDATE AspNetUsers
            SET PhoneNumber = NULL
            WHERE PhoneNumber = '' OR LEN(PhoneNumber) > 32;

            UPDATE AspNetUsers
            SET ParentPhoneNumber = NULL
            WHERE ParentPhoneNumber = '' OR LEN(ParentPhoneNumber) > 32;
            """);

        // Older versions relied only on an application-level check. Preserve one
        // deterministic owner and clear duplicate phone values before adding the
        // database constraint; accounts themselves are never deleted.
        migrationBuilder.Sql("""
            WITH DuplicateUserPhones AS
            (
                SELECT Id,
                       ROW_NUMBER() OVER
                       (
                           PARTITION BY PhoneNumber
                           ORDER BY Id
                       ) AS RowNumber
                FROM AspNetUsers
                WHERE PhoneNumber IS NOT NULL AND PhoneNumber <> ''
            )
            UPDATE AspNetUsers
            SET PhoneNumber = NULL
            WHERE Id IN
            (
                SELECT Id
                FROM DuplicateUserPhones
                WHERE RowNumber > 1
            );
            """);

        migrationBuilder.AlterColumn<string>(
            name: "PhoneNumber",
            table: "AspNetUsers",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ParentPhoneNumber",
            table: "AspNetUsers",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUsers_PhoneNumber",
            table: "AspNetUsers",
            column: "PhoneNumber",
            unique: true,
            filter: "[PhoneNumber] IS NOT NULL AND [PhoneNumber] <> ''");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUsers_ParentPhoneNumber",
            table: "AspNetUsers",
            column: "ParentPhoneNumber",
            filter: "[ParentPhoneNumber] IS NOT NULL AND [ParentPhoneNumber] <> ''");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AspNetUsers_PhoneNumber",
            table: "AspNetUsers");

        migrationBuilder.DropIndex(
            name: "IX_AspNetUsers_ParentPhoneNumber",
            table: "AspNetUsers");

        migrationBuilder.AlterColumn<string>(
            name: "PhoneNumber",
            table: "AspNetUsers",
            type: "nvarchar(max)",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(32)",
            oldMaxLength: 32,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ParentPhoneNumber",
            table: "AspNetUsers",
            type: "nvarchar(max)",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(32)",
            oldMaxLength: 32,
            oldNullable: true);
    }
}
