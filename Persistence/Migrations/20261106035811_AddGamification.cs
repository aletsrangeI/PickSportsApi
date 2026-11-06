using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGamification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EquippedTitleCode",
                table: "Users",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GamificationEnabled",
                table: "Quinielas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UserAchievements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    QuinielaId = table.Column<int>(type: "integer", nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    IsSeen = table.Column<bool>(type: "boolean", nullable: false),
                    SeenAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAchievements_Quinielas_QuinielaId",
                        column: x => x.QuinielaId,
                        principalTable: "Quinielas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAchievements_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAchievements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_QuinielaId",
                table: "UserAchievements",
                column: "QuinielaId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_SeasonId",
                table: "UserAchievements",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_Code_SeasonId",
                table: "UserAchievements",
                columns: new[] { "UserId", "Code", "SeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_IsSeen",
                table: "UserAchievements",
                columns: new[] { "UserId", "IsSeen" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAchievements");

            migrationBuilder.DropColumn(
                name: "EquippedTitleCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GamificationEnabled",
                table: "Quinielas");
        }
    }
}
