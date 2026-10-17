using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuinielaChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuinielaChallenges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuinielaId = table.Column<int>(type: "integer", nullable: false),
                    WeekId = table.Column<int>(type: "integer", nullable: false),
                    CreatorMemberId = table.Column<int>(type: "integer", nullable: false),
                    ChallengeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PayoutMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "WINNER_TAKES_ALL"),
                    StakesDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetMatchId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PENDING_ACCEPTANCE"),
                    ResolutionSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DoubleLifeEventsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuinielaChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuinielaChallenges_Matches_TargetMatchId",
                        column: x => x.TargetMatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QuinielaChallenges_QuinielaMembers_CreatorMemberId",
                        column: x => x.CreatorMemberId,
                        principalTable: "QuinielaMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuinielaChallenges_Quinielas_QuinielaId",
                        column: x => x.QuinielaId,
                        principalTable: "Quinielas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuinielaChallenges_Weeks_WeekId",
                        column: x => x.WeekId,
                        principalTable: "Weeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuinielaChallengeParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChallengeId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PENDING"),
                    PredictedWinnerAbbr = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PredictedHomeScore = table.Column<int>(type: "integer", nullable: true),
                    PredictedAwayScore = table.Column<int>(type: "integer", nullable: true),
                    PredictedTotalGoals = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "NONE"),
                    EliminatedAtMatchId = table.Column<int>(type: "integer", nullable: true),
                    IsDebtSettled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SettledAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuinielaChallengeParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuinielaChallengeParticipants_Matches_EliminatedAtMatchId",
                        column: x => x.EliminatedAtMatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QuinielaChallengeParticipants_QuinielaChallenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "QuinielaChallenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuinielaChallengeParticipants_QuinielaMembers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "QuinielaMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuinielaChallengeParticipants_ChallengeId_MemberId",
                table: "QuinielaChallengeParticipants",
                columns: new[] { "ChallengeId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuinielaChallengeParticipants_EliminatedAtMatchId",
                table: "QuinielaChallengeParticipants",
                column: "EliminatedAtMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_QuinielaChallengeParticipants_MemberId",
                table: "QuinielaChallengeParticipants",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_QuinielaChallenges_CreatorMemberId",
                table: "QuinielaChallenges",
                column: "CreatorMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_QuinielaChallenges_QuinielaId",
                table: "QuinielaChallenges",
                column: "QuinielaId");

            migrationBuilder.CreateIndex(
                name: "IX_QuinielaChallenges_TargetMatchId",
                table: "QuinielaChallenges",
                column: "TargetMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_QuinielaChallenges_WeekId",
                table: "QuinielaChallenges",
                column: "WeekId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuinielaChallengeParticipants");

            migrationBuilder.DropTable(
                name: "QuinielaChallenges");
        }
    }
}
