using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnockoutStrategyPicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Conference",
                table: "LiguillaNodes",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EspnEventGroupKey",
                table: "LiguillaNodes",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KnockoutFormatCode",
                table: "Leagues",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LiguillaBracketPicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BracketId = table.Column<int>(type: "integer", nullable: false),
                    RoundCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiguillaBracketPicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiguillaBracketPicks_LiguillaBrackets_BracketId",
                        column: x => x.BracketId,
                        principalTable: "LiguillaBrackets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiguillaBracketPicks_BracketId_RoundCode_Slot",
                table: "LiguillaBracketPicks",
                columns: new[] { "BracketId", "RoundCode", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiguillaBracketPicks_BracketId_RoundCode_TeamId",
                table: "LiguillaBracketPicks",
                columns: new[] { "BracketId", "RoundCode", "TeamId" },
                unique: true);

            // Liga MX ya tenia Liguilla: marca su formato y copia los picks de las columnas legadas
            // al modelo generico sin cambiar puntos (las columnas legadas se conservan).
            migrationBuilder.Sql(@"UPDATE ""Leagues"" SET ""KnockoutFormatCode"" = 'FMF_LIGUILLA'
WHERE ""Code"" = 'mex.1' AND ""KnockoutFormatCode"" IS NULL;");

            // NFL, Champions League y NHL tambien tienen Pick'em de eliminatorias (idempotente: solo si no tienen formato).
            migrationBuilder.Sql(@"UPDATE ""Leagues"" SET ""KnockoutFormatCode"" = 'NFL_RESEEDED_SINGLE_GAME'
WHERE ""Code"" = 'nfl' AND ""KnockoutFormatCode"" IS NULL;");

            migrationBuilder.Sql(@"UPDATE ""Leagues"" SET ""KnockoutFormatCode"" = 'UEFA_TWO_LEG'
WHERE ""Code"" = 'uefa.champions' AND ""KnockoutFormatCode"" IS NULL;");

            migrationBuilder.Sql(@"UPDATE ""Leagues"" SET ""KnockoutFormatCode"" = 'NHL_BEST_OF_7'
WHERE ""Code"" = 'nhl' AND ""KnockoutFormatCode"" IS NULL;");

            migrationBuilder.Sql(@"INSERT INTO ""LiguillaBracketPicks"" (""BracketId"", ""RoundCode"", ""Slot"", ""TeamId"", ""Created"", ""CreatedBy"", ""LastModifiedBy"", ""Active"")
SELECT b.""Id"", p.""RoundCode"", p.""Slot"", p.""TeamId"", b.""Created"", COALESCE(b.""CreatedBy"", 'migration'), '', TRUE
FROM ""LiguillaBrackets"" b
CROSS JOIN LATERAL (VALUES
    ('QF', 1, b.""QfWinner1TeamId""),
    ('QF', 2, b.""QfWinner2TeamId""),
    ('QF', 3, b.""QfWinner3TeamId""),
    ('QF', 4, b.""QfWinner4TeamId""),
    ('SF', 1, b.""SfWinner1TeamId""),
    ('SF', 2, b.""SfWinner2TeamId""),
    ('FINAL', 1, b.""ChampionTeamId"")
) AS p(""RoundCode"", ""Slot"", ""TeamId"")
WHERE p.""TeamId"" > 0
  AND NOT EXISTS (SELECT 1 FROM ""LiguillaBracketPicks"" x WHERE x.""BracketId"" = b.""Id"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiguillaBracketPicks");

            migrationBuilder.DropColumn(
                name: "Conference",
                table: "LiguillaNodes");

            migrationBuilder.DropColumn(
                name: "EspnEventGroupKey",
                table: "LiguillaNodes");

            migrationBuilder.DropColumn(
                name: "KnockoutFormatCode",
                table: "Leagues");
        }
    }
}
