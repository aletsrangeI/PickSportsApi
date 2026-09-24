using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPickSportsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContenidoCatalgos_Catalogos_CatalogoId",
                table: "ContenidoCatalgos");

            migrationBuilder.DropForeignKey(
                name: "FK_FormFields_ContenidoCatalgos_FormularioId",
                table: "FormFields");

            migrationBuilder.DropForeignKey(
                name: "FK_Quiniela_ContenidoCatalgos_IdLiga",
                table: "Quiniela");

            migrationBuilder.DropForeignKey(
                name: "FK_Quiniela_Users_OwnerId",
                table: "Quiniela");

            migrationBuilder.DropTable(
                name: "EquipoLigas");

            migrationBuilder.DropTable(
                name: "Pronostico");

            migrationBuilder.DropTable(
                name: "UsuariosQuiniela");

            migrationBuilder.DropTable(
                name: "Partido");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Quiniela",
                table: "Quiniela");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ContenidoCatalgos",
                table: "ContenidoCatalgos");

            migrationBuilder.RenameTable(
                name: "Quiniela",
                newName: "Quinielas");

            migrationBuilder.RenameTable(
                name: "ContenidoCatalgos",
                newName: "ContenidoCatalogos");

            migrationBuilder.RenameColumn(
                name: "IdLiga",
                table: "Quinielas",
                newName: "LeagueId");

            migrationBuilder.RenameIndex(
                name: "IX_Quiniela_OwnerId",
                table: "Quinielas",
                newName: "IX_Quinielas_OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_Quiniela_IdLiga",
                table: "Quinielas",
                newName: "IX_Quinielas_LeagueId");

            migrationBuilder.RenameIndex(
                name: "IX_ContenidoCatalgos_CatalogoId",
                table: "ContenidoCatalogos",
                newName: "IX_ContenidoCatalogos_CatalogoId");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "Users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "USER",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Quinielas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EntryFee",
                table: "Quinielas",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "FirstPlacePct",
                table: "Quinielas",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 70.00m);

            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "Quinielas",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Quinielas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Quinielas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SecondPlacePct",
                table: "Quinielas",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 20.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "ThirdPlacePct",
                table: "Quinielas",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 10.00m);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Quinielas",
                table: "Quinielas",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ContenidoCatalogos",
                table: "ContenidoCatalogos",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "EspnHealthLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EndpointTested = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EspnHealthLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Endpoint = table.Column<string>(type: "text", nullable: false),
                    P256dhKey = table.Column<string>(type: "text", nullable: false),
                    AuthKey = table.Column<string>(type: "text", nullable: false),
                    DeviceDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastNotifiedUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuinielaMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuinielaId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Alias = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "MEMBER"),
                    PaidFee = table.Column<bool>(type: "boolean", nullable: false),
                    TotalHits = table.Column<int>(type: "integer", nullable: false),
                    TotalUpsets = table.Column<int>(type: "integer", nullable: false),
                    TotalHumillaciones = table.Column<int>(type: "integer", nullable: false),
                    CurrentStreak = table.Column<int>(type: "integer", nullable: false),
                    BestStreak = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuinielaMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuinielaMembers_Quinielas_QuinielaId",
                        column: x => x.QuinielaId,
                        principalTable: "Quinielas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuinielaMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HasDraw = table.Column<bool>(type: "boolean", nullable: false),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Leagues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SportId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EspnApiPath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leagues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leagues_Sports_SportId",
                        column: x => x.SportId,
                        principalTable: "Sports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    SeasonType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Seasons_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    EspnTeamId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PrimaryColor = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Weeks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    WeekNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "DRAFT"),
                    FirstGameUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LockedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ScoredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Weeks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Weeks_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WeekId = table.Column<int>(type: "integer", nullable: false),
                    EspnGameId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    HomeTeamId = table.Column<int>(type: "integer", nullable: false),
                    AwayTeamId = table.Column<int>(type: "integer", nullable: false),
                    StatusState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pre"),
                    StatusDesc = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HomeScore = table.Column<int>(type: "integer", nullable: false),
                    AwayScore = table.Column<int>(type: "integer", nullable: false),
                    WinnerAbbr = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsUpset = table.Column<bool>(type: "boolean", nullable: false),
                    Venue = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastSyncUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matches_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Weeks_WeekId",
                        column: x => x.WeekId,
                        principalTable: "Weeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyAwards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuinielaId = table.Column<int>(type: "integer", nullable: false),
                    WeekId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    AwardType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AwardValue1 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AwardValue2 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyAwards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyAwards_QuinielaMembers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "QuinielaMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WeeklyAwards_Quinielas_QuinielaId",
                        column: x => x.QuinielaId,
                        principalTable: "Quinielas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WeeklyAwards_Weeks_WeekId",
                        column: x => x.WeekId,
                        principalTable: "Weeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PickAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuinielaId = table.Column<int>(type: "integer", nullable: false),
                    WeekId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    MatchId = table.Column<int>(type: "integer", nullable: false),
                    PickAbbr = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "MANUAL"),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickAuditLogs_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PickAuditLogs_QuinielaMembers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "QuinielaMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PickAuditLogs_Quinielas_QuinielaId",
                        column: x => x.QuinielaId,
                        principalTable: "Quinielas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PickAuditLogs_Weeks_WeekId",
                        column: x => x.WeekId,
                        principalTable: "Weeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Picks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuinielaId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    MatchId = table.Column<int>(type: "integer", nullable: false),
                    PickAbbr = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsAutoFilled = table.Column<bool>(type: "boolean", nullable: false),
                    IsHit = table.Column<bool>(type: "boolean", nullable: true),
                    IsUpsetHit = table.Column<bool>(type: "boolean", nullable: false),
                    IsHumillacion = table.Column<bool>(type: "boolean", nullable: false),
                    IsSomnifero = table.Column<bool>(type: "boolean", nullable: false),
                    IsEmpateFallido = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Picks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Picks_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Picks_QuinielaMembers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "QuinielaMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Picks_Quinielas_QuinielaId",
                        column: x => x.QuinielaId,
                        principalTable: "Quinielas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quinielas_InviteCode",
                table: "Quinielas",
                column: "InviteCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EspnHealthLogs_CheckedAt",
                table: "EspnHealthLogs",
                column: "CheckedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EspnHealthLogs_IsSuccess",
                table: "EspnHealthLogs",
                column: "IsSuccess");

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_Code",
                table: "Leagues",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_SportId",
                table: "Leagues",
                column: "SportId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_AwayTeamId",
                table: "Matches",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_DateUtc",
                table: "Matches",
                column: "DateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_EspnGameId",
                table: "Matches",
                column: "EspnGameId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matches_HomeTeamId",
                table: "Matches",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_WeekId",
                table: "Matches",
                column: "WeekId");

            migrationBuilder.CreateIndex(
                name: "IX_PickAuditLogs_MatchId",
                table: "PickAuditLogs",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PickAuditLogs_MemberId",
                table: "PickAuditLogs",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_PickAuditLogs_QuinielaId_WeekId",
                table: "PickAuditLogs",
                columns: new[] { "QuinielaId", "WeekId" });

            migrationBuilder.CreateIndex(
                name: "IX_PickAuditLogs_Timestamp",
                table: "PickAuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_PickAuditLogs_WeekId",
                table: "PickAuditLogs",
                column: "WeekId");

            migrationBuilder.CreateIndex(
                name: "IX_Picks_MatchId",
                table: "Picks",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Picks_MemberId",
                table: "Picks",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Picks_QuinielaId_MemberId_MatchId",
                table: "Picks",
                columns: new[] { "QuinielaId", "MemberId", "MatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_Endpoint",
                table: "PushSubscriptions",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_UserId",
                table: "PushSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuinielaMembers_QuinielaId_UserId",
                table: "QuinielaMembers",
                columns: new[] { "QuinielaId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuinielaMembers_UserId",
                table: "QuinielaMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_LeagueId_Year_SeasonType",
                table: "Seasons",
                columns: new[] { "LeagueId", "Year", "SeasonType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sports_Code",
                table: "Sports",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_LeagueId_Abbreviation",
                table: "Teams",
                columns: new[] { "LeagueId", "Abbreviation" });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_LeagueId_EspnTeamId",
                table: "Teams",
                columns: new[] { "LeagueId", "EspnTeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyAwards_MemberId",
                table: "WeeklyAwards",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyAwards_QuinielaId_WeekId_AwardType",
                table: "WeeklyAwards",
                columns: new[] { "QuinielaId", "WeekId", "AwardType" });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyAwards_WeekId",
                table: "WeeklyAwards",
                column: "WeekId");

            migrationBuilder.CreateIndex(
                name: "IX_Weeks_SeasonId_WeekNumber",
                table: "Weeks",
                columns: new[] { "SeasonId", "WeekNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContenidoCatalogos_Catalogos_CatalogoId",
                table: "ContenidoCatalogos",
                column: "CatalogoId",
                principalTable: "Catalogos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FormFields_ContenidoCatalogos_FormularioId",
                table: "FormFields",
                column: "FormularioId",
                principalTable: "ContenidoCatalogos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Quinielas_Leagues_LeagueId",
                table: "Quinielas",
                column: "LeagueId",
                principalTable: "Leagues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quinielas_Users_OwnerId",
                table: "Quinielas",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContenidoCatalogos_Catalogos_CatalogoId",
                table: "ContenidoCatalogos");

            migrationBuilder.DropForeignKey(
                name: "FK_FormFields_ContenidoCatalogos_FormularioId",
                table: "FormFields");

            migrationBuilder.DropForeignKey(
                name: "FK_Quinielas_Leagues_LeagueId",
                table: "Quinielas");

            migrationBuilder.DropForeignKey(
                name: "FK_Quinielas_Users_OwnerId",
                table: "Quinielas");

            migrationBuilder.DropTable(
                name: "EspnHealthLogs");

            migrationBuilder.DropTable(
                name: "PickAuditLogs");

            migrationBuilder.DropTable(
                name: "Picks");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.DropTable(
                name: "WeeklyAwards");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "QuinielaMembers");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Weeks");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropTable(
                name: "Leagues");

            migrationBuilder.DropTable(
                name: "Sports");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Quinielas",
                table: "Quinielas");

            migrationBuilder.DropIndex(
                name: "IX_Quinielas_InviteCode",
                table: "Quinielas");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ContenidoCatalogos",
                table: "ContenidoCatalogos");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Quinielas");

            migrationBuilder.DropColumn(
                name: "EntryFee",
                table: "Quinielas");

            migrationBuilder.DropColumn(
                name: "FirstPlacePct",
                table: "Quinielas");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "Quinielas");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Quinielas");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Quinielas");

            migrationBuilder.DropColumn(
                name: "SecondPlacePct",
                table: "Quinielas");

            migrationBuilder.DropColumn(
                name: "ThirdPlacePct",
                table: "Quinielas");

            migrationBuilder.RenameTable(
                name: "Quinielas",
                newName: "Quiniela");

            migrationBuilder.RenameTable(
                name: "ContenidoCatalogos",
                newName: "ContenidoCatalgos");

            migrationBuilder.RenameColumn(
                name: "LeagueId",
                table: "Quiniela",
                newName: "IdLiga");

            migrationBuilder.RenameIndex(
                name: "IX_Quinielas_OwnerId",
                table: "Quiniela",
                newName: "IX_Quiniela_OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_Quinielas_LeagueId",
                table: "Quiniela",
                newName: "IX_Quiniela_IdLiga");

            migrationBuilder.RenameIndex(
                name: "IX_ContenidoCatalogos_CatalogoId",
                table: "ContenidoCatalgos",
                newName: "IX_ContenidoCatalgos_CatalogoId");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "Users",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "USER");

            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Quiniela",
                table: "Quiniela",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ContenidoCatalgos",
                table: "ContenidoCatalgos",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "EquipoLigas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipoId = table.Column<int>(type: "integer", nullable: false),
                    LigaId = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipoLigas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipoLigas_ContenidoCatalgos_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "ContenidoCatalgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EquipoLigas_ContenidoCatalgos_LigaId",
                        column: x => x.LigaId,
                        principalTable: "ContenidoCatalgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Partido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdLocal = table.Column<int>(type: "integer", nullable: false),
                    IdVisitante = table.Column<int>(type: "integer", nullable: false),
                    LigaId = table.Column<int>(type: "integer", nullable: false),
                    Resultado = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Partido_ContenidoCatalgos_IdLocal",
                        column: x => x.IdLocal,
                        principalTable: "ContenidoCatalgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Partido_ContenidoCatalgos_IdVisitante",
                        column: x => x.IdVisitante,
                        principalTable: "ContenidoCatalgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Partido_ContenidoCatalgos_LigaId",
                        column: x => x.LigaId,
                        principalTable: "ContenidoCatalgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Partido_ContenidoCatalgos_Resultado",
                        column: x => x.Resultado,
                        principalTable: "ContenidoCatalgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosQuiniela",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuinielaId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosQuiniela", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuariosQuiniela_Quiniela_QuinielaId",
                        column: x => x.QuinielaId,
                        principalTable: "Quiniela",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuariosQuiniela_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pronostico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartidoId = table.Column<int>(type: "integer", nullable: false),
                    PronosticoUsuario = table.Column<int>(type: "integer", nullable: false),
                    QuinielaId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pronostico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pronostico_ContenidoCatalgos_PronosticoUsuario",
                        column: x => x.PronosticoUsuario,
                        principalTable: "ContenidoCatalgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pronostico_Partido_PartidoId",
                        column: x => x.PartidoId,
                        principalTable: "Partido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pronostico_Quiniela_QuinielaId",
                        column: x => x.QuinielaId,
                        principalTable: "Quiniela",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pronostico_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquipoLigas_EquipoId",
                table: "EquipoLigas",
                column: "EquipoId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipoLigas_LigaId",
                table: "EquipoLigas",
                column: "LigaId");

            migrationBuilder.CreateIndex(
                name: "IX_Partido_IdLocal",
                table: "Partido",
                column: "IdLocal");

            migrationBuilder.CreateIndex(
                name: "IX_Partido_IdVisitante",
                table: "Partido",
                column: "IdVisitante");

            migrationBuilder.CreateIndex(
                name: "IX_Partido_LigaId",
                table: "Partido",
                column: "LigaId");

            migrationBuilder.CreateIndex(
                name: "IX_Partido_Resultado",
                table: "Partido",
                column: "Resultado");

            migrationBuilder.CreateIndex(
                name: "IX_Pronostico_PartidoId",
                table: "Pronostico",
                column: "PartidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pronostico_PronosticoUsuario",
                table: "Pronostico",
                column: "PronosticoUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_Pronostico_QuinielaId",
                table: "Pronostico",
                column: "QuinielaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pronostico_UserId",
                table: "Pronostico",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosQuiniela_QuinielaId",
                table: "UsuariosQuiniela",
                column: "QuinielaId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosQuiniela_UserId",
                table: "UsuariosQuiniela",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContenidoCatalgos_Catalogos_CatalogoId",
                table: "ContenidoCatalgos",
                column: "CatalogoId",
                principalTable: "Catalogos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FormFields_ContenidoCatalgos_FormularioId",
                table: "FormFields",
                column: "FormularioId",
                principalTable: "ContenidoCatalgos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Quiniela_ContenidoCatalgos_IdLiga",
                table: "Quiniela",
                column: "IdLiga",
                principalTable: "ContenidoCatalgos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Quiniela_Users_OwnerId",
                table: "Quiniela",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
