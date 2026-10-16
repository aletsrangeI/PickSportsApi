using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomEventsDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "LeagueId",
                table: "Quinielas",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "QuinielaType",
                table: "Quinielas",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TotalPoints",
                table: "QuinielaMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EventGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuinielaId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventGroups_Quinielas_QuinielaId",
                        column: x => x.QuinielaId,
                        principalTable: "Quinielas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventRounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventGroupId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RoundOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    LockDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Open"),
                    ResolvedDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventRounds_EventGroups_EventGroupId",
                        column: x => x.EventGroupId,
                        principalTable: "EventGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventRoundId = table.Column<int>(type: "integer", nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Points = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    QuestionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "SingleChoice"),
                    QuestionOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventQuestions_EventRounds_EventRoundId",
                        column: x => x.EventRoundId,
                        principalTable: "EventRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventQuestionId = table.Column<int>(type: "integer", nullable: false),
                    OptionText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    OptionOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventOptions_EventQuestions_EventQuestionId",
                        column: x => x.EventQuestionId,
                        principalTable: "EventQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventPicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventQuestionId = table.Column<int>(type: "integer", nullable: false),
                    EventOptionId = table.Column<int>(type: "integer", nullable: false),
                    QuinielaMemberId = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsHit = table.Column<bool>(type: "boolean", nullable: true),
                    PointsEarned = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventPicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventPicks_EventOptions_EventOptionId",
                        column: x => x.EventOptionId,
                        principalTable: "EventOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventPicks_EventQuestions_EventQuestionId",
                        column: x => x.EventQuestionId,
                        principalTable: "EventQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventPicks_QuinielaMembers_QuinielaMemberId",
                        column: x => x.QuinielaMemberId,
                        principalTable: "QuinielaMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventGroups_QuinielaId",
                table: "EventGroups",
                column: "QuinielaId");

            migrationBuilder.CreateIndex(
                name: "IX_EventOptions_EventQuestionId",
                table: "EventOptions",
                column: "EventQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_EventPicks_EventOptionId",
                table: "EventPicks",
                column: "EventOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_EventPicks_EventQuestionId_QuinielaMemberId",
                table: "EventPicks",
                columns: new[] { "EventQuestionId", "QuinielaMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventPicks_QuinielaMemberId",
                table: "EventPicks",
                column: "QuinielaMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_EventQuestions_EventRoundId",
                table: "EventQuestions",
                column: "EventRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_EventRounds_EventGroupId",
                table: "EventRounds",
                column: "EventGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventPicks");

            migrationBuilder.DropTable(
                name: "EventOptions");

            migrationBuilder.DropTable(
                name: "EventQuestions");

            migrationBuilder.DropTable(
                name: "EventRounds");

            migrationBuilder.DropTable(
                name: "EventGroups");

            migrationBuilder.DropColumn(
                name: "QuinielaType",
                table: "Quinielas");

            migrationBuilder.DropColumn(
                name: "TotalPoints",
                table: "QuinielaMembers");

            migrationBuilder.AlterColumn<int>(
                name: "LeagueId",
                table: "Quinielas",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
