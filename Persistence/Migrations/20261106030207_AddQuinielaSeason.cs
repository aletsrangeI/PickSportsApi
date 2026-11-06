using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuinielaSeason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Quinielas",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeasonId",
                table: "Quinielas",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quinielas_SeasonId",
                table: "Quinielas",
                column: "SeasonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quinielas_Seasons_SeasonId",
                table: "Quinielas",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // SPEC-020 backfill: temporada del pick mas reciente de la quiniela (lo mismo que deducia
            // QuinielaSeasonResolver) y, si no tiene picks, la temporada vigente de su liga.
            // Solo escribe Quinielas.SeasonId; no toca Picks, WeeklyAwards, WeeklyBulletins ni ninguna otra tabla.
            // QuinielaType: 1 = Sports. Eventos Personalizados (o sin liga) queda en NULL.
            migrationBuilder.Sql("""
                UPDATE "Quinielas" q SET "SeasonId" = COALESCE(
                    (SELECT w."SeasonId"
                       FROM "Picks" p
                       JOIN "Matches" m ON m."Id" = p."MatchId"
                       JOIN "Weeks" w ON w."Id" = m."WeekId"
                      WHERE p."QuinielaId" = q."Id"
                      ORDER BY m."DateUtc" DESC
                      LIMIT 1),
                    (SELECT s."Id"
                       FROM "Seasons" s
                      WHERE s."LeagueId" = q."LeagueId" AND s."IsCurrent"
                      ORDER BY s."Id" DESC
                      LIMIT 1))
                WHERE q."SeasonId" IS NULL AND q."LeagueId" IS NOT NULL AND q."QuinielaType" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quinielas_Seasons_SeasonId",
                table: "Quinielas");

            migrationBuilder.DropIndex(
                name: "IX_Quinielas_SeasonId",
                table: "Quinielas");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Quinielas");

            migrationBuilder.DropColumn(
                name: "SeasonId",
                table: "Quinielas");
        }
    }
}
