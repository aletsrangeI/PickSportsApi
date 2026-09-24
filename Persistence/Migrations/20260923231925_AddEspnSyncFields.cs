using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEspnSyncFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Seasons",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Seasons",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PostponedToDate",
                table: "Matches",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SportKey",
                table: "Leagues",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "soccer");

            migrationBuilder.AddColumn<int>(
                name: "WeekDurationDays",
                table: "Leagues",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "WeeksCount",
                table: "Leagues",
                type: "integer",
                nullable: false,
                defaultValue: 17);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PostponedToDate",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "SportKey",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "WeekDurationDays",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "WeeksCount",
                table: "Leagues");
        }
    }
}
