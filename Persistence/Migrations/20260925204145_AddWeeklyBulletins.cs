using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyBulletins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeeklyBulletins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuinielaId = table.Column<int>(type: "integer", nullable: false),
                    WeekId = table.Column<int>(type: "integer", nullable: false),
                    AdminAnnouncement = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyBulletins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyBulletins_Quinielas_QuinielaId",
                        column: x => x.QuinielaId,
                        principalTable: "Quinielas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WeeklyBulletins_Weeks_WeekId",
                        column: x => x.WeekId,
                        principalTable: "Weeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyBulletins_QuinielaId_WeekId",
                table: "WeeklyBulletins",
                columns: new[] { "QuinielaId", "WeekId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyBulletins_WeekId",
                table: "WeeklyBulletins",
                column: "WeekId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeeklyBulletins");
        }
    }
}
