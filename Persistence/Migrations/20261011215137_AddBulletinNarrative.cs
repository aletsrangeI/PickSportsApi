using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBulletinNarrative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Chronicle",
                table: "WeeklyBulletins",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GeneratedByAi",
                table: "WeeklyBulletins",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Headline",
                table: "WeeklyBulletins",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NarrativeModel",
                table: "WeeklyBulletins",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Chronicle",
                table: "WeeklyBulletins");

            migrationBuilder.DropColumn(
                name: "GeneratedByAi",
                table: "WeeklyBulletins");

            migrationBuilder.DropColumn(
                name: "Headline",
                table: "WeeklyBulletins");

            migrationBuilder.DropColumn(
                name: "NarrativeModel",
                table: "WeeklyBulletins");
        }
    }
}
