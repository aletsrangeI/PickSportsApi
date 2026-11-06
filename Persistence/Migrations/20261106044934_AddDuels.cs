using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDuels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DuelFixtures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuinielaId = table.Column<int>(type: "integer", nullable: false),
                    WeekNumber = table.Column<int>(type: "integer", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Leg = table.Column<int>(type: "integer", nullable: false),
                    HomeMemberId = table.Column<int>(type: "integer", nullable: false),
                    AwayMemberId = table.Column<int>(type: "integer", nullable: true),
                    HomeHits = table.Column<int>(type: "integer", nullable: false),
                    AwayHits = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    HomeRankBefore = table.Column<int>(type: "integer", nullable: true),
                    AwayRankBefore = table.Column<int>(type: "integer", nullable: true),
                    WonByAdvantage = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuelFixtures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DuelFixtures_QuinielaMembers_AwayMemberId",
                        column: x => x.AwayMemberId,
                        principalTable: "QuinielaMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuelFixtures_QuinielaMembers_HomeMemberId",
                        column: x => x.HomeMemberId,
                        principalTable: "QuinielaMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuelFixtures_Quinielas_QuinielaId",
                        column: x => x.QuinielaId,
                        principalTable: "Quinielas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuinielaDuelSeasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuinielaId = table.Column<int>(type: "integer", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ChampionMemberId = table.Column<int>(type: "integer", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CupName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PunishmentText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuinielaDuelSeasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuinielaDuelSeasons_QuinielaMembers_ChampionMemberId",
                        column: x => x.ChampionMemberId,
                        principalTable: "QuinielaMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuinielaDuelSeasons_Quinielas_QuinielaId",
                        column: x => x.QuinielaId,
                        principalTable: "Quinielas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DuelFixtures_AwayMemberId",
                table: "DuelFixtures",
                column: "AwayMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_DuelFixtures_HomeMemberId",
                table: "DuelFixtures",
                column: "HomeMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_DuelFixtures_QuinielaId_WeekNumber",
                table: "DuelFixtures",
                columns: new[] { "QuinielaId", "WeekNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_DuelFixtures_QuinielaId_WeekNumber_HomeMemberId",
                table: "DuelFixtures",
                columns: new[] { "QuinielaId", "WeekNumber", "HomeMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuinielaDuelSeasons_ChampionMemberId",
                table: "QuinielaDuelSeasons",
                column: "ChampionMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_QuinielaDuelSeasons_QuinielaId",
                table: "QuinielaDuelSeasons",
                column: "QuinielaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DuelFixtures");

            migrationBuilder.DropTable(
                name: "QuinielaDuelSeasons");
        }
    }
}
