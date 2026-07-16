using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ANU_Admissions.Migrations
{
    /// <inheritdoc />
    public partial class AddParentPhoneNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParentPhoneNumber",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentPhoneNumber",
                table: "AspNetUsers");
        }
    }
}
