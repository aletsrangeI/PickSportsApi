using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLiguillaBracketChallenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiguillaBrackets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuinielaId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalScore = table.Column<int>(type: "integer", nullable: false),
                    QfWinner1TeamId = table.Column<int>(type: "integer", nullable: false),
                    QfWinner2TeamId = table.Column<int>(type: "integer", nullable: false),
                    QfWinner3TeamId = table.Column<int>(type: "integer", nullable: false),
                    QfWinner4TeamId = table.Column<int>(type: "integer", nullable: false),
                    SfWinner1TeamId = table.Column<int>(type: "integer", nullable: false),
                    SfWinner2TeamId = table.Column<int>(type: "integer", nullable: false),
                    ChampionTeamId = table.Column<int>(type: "integer", nullable: false),
                    QfPoints = table.Column<int>(type: "integer", nullable: false),
                    SfPoints = table.Column<int>(type: "integer", nullable: false),
                    ChampionPoints = table.Column<int>(type: "integer", nullable: false),
                    BonusPoints = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiguillaBrackets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiguillaBrackets_Quinielas_QuinielaId",
                        column: x => x.QuinielaId,
                        principalTable: "Quinielas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LiguillaBrackets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiguillaNodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    Round = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NodeNumber = table.Column<int>(type: "integer", nullable: false),
                    TeamAId = table.Column<int>(type: "integer", nullable: true),
                    TeamBId = table.Column<int>(type: "integer", nullable: true),
                    SeedA = table.Column<int>(type: "integer", nullable: true),
                    SeedB = table.Column<int>(type: "integer", nullable: true),
                    WinnerTeamId = table.Column<int>(type: "integer", nullable: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    KickoffUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiguillaNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiguillaNodes_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LiguillaNodes_Teams_TeamAId",
                        column: x => x.TeamAId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LiguillaNodes_Teams_TeamBId",
                        column: x => x.TeamBId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LiguillaNodes_Teams_WinnerTeamId",
                        column: x => x.WinnerTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiguillaBrackets_QuinielaId_UserId",
                table: "LiguillaBrackets",
                columns: new[] { "QuinielaId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiguillaBrackets_UserId",
                table: "LiguillaBrackets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LiguillaNodes_SeasonId_NodeNumber",
                table: "LiguillaNodes",
                columns: new[] { "SeasonId", "NodeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiguillaNodes_TeamAId",
                table: "LiguillaNodes",
                column: "TeamAId");

            migrationBuilder.CreateIndex(
                name: "IX_LiguillaNodes_TeamBId",
                table: "LiguillaNodes",
                column: "TeamBId");

            migrationBuilder.CreateIndex(
                name: "IX_LiguillaNodes_WinnerTeamId",
                table: "LiguillaNodes",
                column: "WinnerTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiguillaBrackets");

            migrationBuilder.DropTable(
                name: "LiguillaNodes");
        }
    }
}
