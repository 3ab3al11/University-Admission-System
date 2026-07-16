using ANU_Admissions.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ANU_Admissions.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260713000000_PreventDuplicateCollegePreferences")]
public partial class PreventDuplicateCollegePreferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Older versions allowed the same college at different ranks. Keep its
        // highest-ranked occurrence before enforcing the new business rule.
        migrationBuilder.Sql("""
            WITH DuplicatePreferences AS
            (
                SELECT Id,
                       ROW_NUMBER() OVER
                       (
                           PARTITION BY StudentProfileId, CollegeId
                           ORDER BY Rank, Id
                       ) AS RowNumber
                FROM Preferences
            )
            DELETE FROM Preferences
            WHERE Id IN
            (
                SELECT Id
                FROM DuplicatePreferences
                WHERE RowNumber > 1
            );
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Preferences_StudentProfileId_CollegeId",
            table: "Preferences",
            columns: new[] { "StudentProfileId", "CollegeId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Preferences_StudentProfileId_CollegeId",
            table: "Preferences");
    }
}
