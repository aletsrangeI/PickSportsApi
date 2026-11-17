using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixNflRegularSeasonAndWeeks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La temporada regular de la NFL tiene 18 semanas: con 17 la semana 18 se trataba como fase final y quedaba oculta.
            migrationBuilder.Sql(@"UPDATE ""Leagues"" SET ""WeeksCount"" = 18 WHERE ""Code"" = 'nfl' AND ""WeeksCount"" = 17;");

            // La semilla creaba la temporada NFL con tipo ESPN 1 (pretemporada); la regular es el tipo 2.
            // Tras aplicar esto hay que re-sincronizar la temporada ("Sync ESPN") para cargar las semanas regulares correctas.
            migrationBuilder.Sql(@"UPDATE ""Seasons"" SET ""SeasonType"" = 2
WHERE ""SeasonType"" = 1
  AND ""Name"" LIKE 'Temporada Regular%'
  AND ""LeagueId"" IN (SELECT ""Id"" FROM ""Leagues"" WHERE ""Code"" = 'nfl');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""Seasons"" SET ""SeasonType"" = 1
WHERE ""SeasonType"" = 2
  AND ""Name"" LIKE 'Temporada Regular%'
  AND ""LeagueId"" IN (SELECT ""Id"" FROM ""Leagues"" WHERE ""Code"" = 'nfl');");

            migrationBuilder.Sql(@"UPDATE ""Leagues"" SET ""WeeksCount"" = 17 WHERE ""Code"" = 'nfl' AND ""WeeksCount"" = 18;");
        }
    }
}
