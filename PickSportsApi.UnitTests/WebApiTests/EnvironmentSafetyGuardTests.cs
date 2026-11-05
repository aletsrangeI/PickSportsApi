using Microsoft.Extensions.Configuration;
using WebApi.Modules.Safety;
using Xunit;

namespace PickSportsApi.UnitTests.WebApiTests;

public class EnvironmentSafetyGuardTests
{
    private const string TestDbCs = "Server=h;Port=5432;Database=picksports_test;User Id=u;Password=p;";
    private const string ProdDbCs = "Server=h;Port=5432;Database=picksports;User Id=u;Password=p;";
    private const string Secret = "test-only-secret-0123456789abcdef0123456789abcdef0123456789abcdef";

    // Aísla las pruebas de las variables de entorno reales de la máquina.
    private static readonly Func<string, string?> NoEnv = _ => null;

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    private static IConfiguration ProductionConfig(string connectionString = ProdDbCs) => Config(
        ("ConnectionStrings:PickSports", connectionString),
        ("Config:Secret", Secret),
        ("WatchDog:WatchPagePassword", "a-strong-password"),
        ("Safety:AllowProduction", "true"));

    private const string DevOnProdDbMessage =
        "EnvironmentSafety: un entorno no productivo no puede usar la base de producción 'picksports'.";

    // ---------- R3: un entorno no productivo no puede usar la base de producción ----------

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void Validate_NonProductionOnProductionDatabase_ReturnsR3(string environment)
    {
        // Arrange
        var config = Config(("ConnectionStrings:PickSports", ProdDbCs), ("Config:Secret", Secret));

        // Act
        var errors = EnvironmentSafetyGuard.Validate(config, environment, NoEnv);

        // Assert
        Assert.Equal(new[] { DevOnProdDbMessage }, errors);
    }

    [Fact]
    public void Validate_NonProductionOnTestDatabase_IsSafe()
    {
        var config = Config(("ConnectionStrings:PickSports", TestDbCs), ("Config:Secret", Secret));

        var errors = EnvironmentSafetyGuard.Validate(config, "Development", NoEnv);

        Assert.Empty(errors); // igualdad exacta: picksports_test NO coincide con picksports
    }

    [Fact]
    public void Validate_DatabaseNameComparisonIgnoresCase()
    {
        var config = Config(
            ("ConnectionStrings:PickSports", "Server=h;Database=PickSports;User Id=u;Password=p;"),
            ("Config:Secret", Secret));

        var errors = EnvironmentSafetyGuard.Validate(config, "development", NoEnv);

        Assert.Equal(new[] { DevOnProdDbMessage }, errors);
    }

    [Fact]
    public void Validate_CustomProductionDatabaseName_IsUsedInRuleAndMessage()
    {
        var config = Config(
            ("ConnectionStrings:PickSports", "Server=h;Database=mi_prod;User Id=u;Password=p;"),
            ("Config:Secret", Secret),
            ("Safety:ProductionDatabaseName", "mi_prod"));

        var errors = EnvironmentSafetyGuard.Validate(config, "Development", NoEnv);

        Assert.Equal(
            new[] { "EnvironmentSafety: un entorno no productivo no puede usar la base de producción 'mi_prod'." },
            errors);
    }

    [Fact]
    public void Validate_UnparseableConnectionString_DoesNotApplyR3()
    {
        var config = Config(("ConnectionStrings:PickSports", "esto no es una cadena de conexion"), ("Config:Secret", Secret));

        var errors = EnvironmentSafetyGuard.Validate(config, "Development", NoEnv);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ConnectionStringWithoutDatabase_UsesUserNameLikeNpgsql_ReturnsR3()
    {
        // Npgsql conecta a la base con el nombre del usuario cuando no hay "Database=".
        var config = Config(
            ("ConnectionStrings:PickSports", "Server=h;Port=5432;User Id=picksports;Password=p;"),
            ("Config:Secret", Secret));

        var errors = EnvironmentSafetyGuard.Validate(config, "Development", NoEnv);

        Assert.Equal(new[] { DevOnProdDbMessage }, errors);
    }

    [Fact]
    public void Validate_ConnectionStringWithoutDatabaseAndOtherUser_IsSafe()
    {
        var config = Config(
            ("ConnectionStrings:PickSports", "Server=h;Port=5432;User Id=picksports_app;Password=p;"),
            ("Config:Secret", Secret));

        var errors = EnvironmentSafetyGuard.Validate(config, "Development", NoEnv);

        Assert.Empty(errors);
    }

    // ---------- R1 / R2: configuración obligatoria ----------

    [Fact]
    public void Validate_MissingConnectionString_ReturnsR1()
    {
        var config = Config(("Config:Secret", Secret));

        var errors = EnvironmentSafetyGuard.Validate(config, "Development", NoEnv);

        Assert.Equal(
            new[] { "EnvironmentSafety: falta la configuración obligatoria 'ConnectionStrings:PickSports'." },
            errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankSecret_ReturnsR2(string? secret)
    {
        var config = Config(("ConnectionStrings:PickSports", TestDbCs), ("Config:Secret", secret));

        var errors = EnvironmentSafetyGuard.Validate(config, "Development", NoEnv);

        Assert.Equal(
            new[] { "EnvironmentSafety: falta la configuración obligatoria 'Config:Secret'." },
            errors);
    }

    [Fact]
    public void Validate_AccumulatesAllErrors_InOrder()
    {
        var config = Config();

        var errors = EnvironmentSafetyGuard.Validate(config, "Development", NoEnv);

        Assert.Equal(2, errors.Count);
        Assert.Contains("ConnectionStrings:PickSports", errors[0]);
        Assert.Contains("Config:Secret", errors[1]);
    }

    // ---------- Production: R4 y R5 ----------

    [Fact]
    public void Validate_ProductionOnProductionDatabaseWithEverything_IsSafe()
    {
        var errors = EnvironmentSafetyGuard.Validate(ProductionConfig(), "Production", NoEnv);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ProductionOnTestDatabase_IsAllowed()
    {
        var errors = EnvironmentSafetyGuard.Validate(ProductionConfig(TestDbCs), "Production", NoEnv);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    public void Validate_ProductionWithWeakWatchDogPassword_ReturnsR4(string? password)
    {
        var config = Config(
            ("ConnectionStrings:PickSports", ProdDbCs),
            ("Config:Secret", Secret),
            ("WatchDog:WatchPagePassword", password),
            ("Safety:AllowProduction", "true"));

        var errors = EnvironmentSafetyGuard.Validate(config, "Production", NoEnv);

        Assert.Equal(
            new[] { "EnvironmentSafety: WatchDog:WatchPagePassword no puede estar vacío ni ser 'admin' en producción." },
            errors);
    }

    [Fact]
    public void Validate_NonProductionIgnoresWatchDogPassword()
    {
        var config = Config(
            ("ConnectionStrings:PickSports", TestDbCs),
            ("Config:Secret", Secret),
            ("WatchDog:WatchPagePassword", "admin"));

        var errors = EnvironmentSafetyGuard.Validate(config, "Development", NoEnv);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("no-es-un-booleano")]
    public void Validate_ProductionWithoutAllowProduction_ReturnsR5(string? allow)
    {
        var config = Config(
            ("ConnectionStrings:PickSports", ProdDbCs),
            ("Config:Secret", Secret),
            ("WatchDog:WatchPagePassword", "a-strong-password"),
            ("Safety:AllowProduction", allow));

        var errors = EnvironmentSafetyGuard.Validate(config, "Production", NoEnv);

        Assert.Equal(
            new[] { "EnvironmentSafety: el entorno Production requiere 'Safety:AllowProduction=true'." },
            errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingEnvironmentName_IsTreatedAsProduction(string? environment)
    {
        // Sin ASPNETCORE_ENVIRONMENT .NET resuelve "Production": no puede esquivar R5 ni caer en R3.
        var config = Config(("ConnectionStrings:PickSports", ProdDbCs), ("Config:Secret", Secret),
            ("WatchDog:WatchPagePassword", "a-strong-password"));

        var errors = EnvironmentSafetyGuard.Validate(config, environment, NoEnv);

        Assert.Equal(
            new[] { "EnvironmentSafety: el entorno Production requiere 'Safety:AllowProduction=true'." },
            errors);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("production")]
    [InlineData("PRODUCTION")]
    public void Validate_ProductionNameIsCaseInsensitive(string environment)
    {
        var errors = EnvironmentSafetyGuard.Validate(ProductionConfig(), environment, NoEnv);

        Assert.Empty(errors);
    }

    // ---------- La guardia valida la misma cadena que usará el DbContext ----------

    [Fact]
    public void Validate_EnvironmentVariableWinsOverConfiguration_LikeTheDbContext()
    {
        // Arrange: la configuración apunta a pruebas pero la variable de entorno a producción.
        var config = Config(("ConnectionStrings:PickSports", TestDbCs), ("Config:Secret", Secret));
        Func<string, string?> env = name => name == "ConnectionStrings__PickSports" ? ProdDbCs : null;

        // Act
        var errors = EnvironmentSafetyGuard.Validate(config, "Development", env);

        // Assert: el DbContext usaría la de producción, y la guardia lo ve.
        Assert.Equal(new[] { DevOnProdDbMessage }, errors);
    }

    [Fact]
    public void Validate_LegacyAliasOnly_SatisfiesR1AndIsChecked()
    {
        var config = Config(("ConnectionStrings:picksports_db", ProdDbCs), ("Config:Secret", Secret));

        var errors = EnvironmentSafetyGuard.Validate(config, "Development", NoEnv);

        Assert.Equal(new[] { DevOnProdDbMessage }, errors); // sin R1: el alias cuenta
    }

    [Fact]
    public void Validate_BothKeys_UsesTheSameOneAsTheDbContext()
    {
        // PickSports (pruebas) tiene prioridad sobre picksports_db (producción): ambas cadenas se resuelven igual.
        var config = Config(
            ("ConnectionStrings:PickSports", TestDbCs),
            ("ConnectionStrings:picksports_db", ProdDbCs),
            ("Config:Secret", Secret));

        var errors = EnvironmentSafetyGuard.Validate(config, "Development", NoEnv);

        Assert.Empty(errors);
    }

    // ---------- EnsureSafe ----------

    [Fact]
    public void EnsureSafe_WhenSafe_DoesNotThrow()
    {
        var config = Config(("ConnectionStrings:PickSports", TestDbCs), ("Config:Secret", Secret));

        var exception = Record.Exception(() => EnvironmentSafetyGuard.EnsureSafe(config, "Development", NoEnv));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureSafe_WhenUnsafe_ThrowsWithMessagesJoinedByNewLine()
    {
        var config = Config();

        var exception = Assert.Throws<InvalidOperationException>(
            () => EnvironmentSafetyGuard.EnsureSafe(config, "Development", NoEnv));

        Assert.Equal(
            "EnvironmentSafety: falta la configuración obligatoria 'ConnectionStrings:PickSports'.\n" +
            "EnvironmentSafety: falta la configuración obligatoria 'Config:Secret'.",
            exception.Message);
    }
}
