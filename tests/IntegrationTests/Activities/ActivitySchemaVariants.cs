using Infrastructure.Persistence.EntityFramework;
using IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Activities;

/// <summary>
/// The two measured schema variants of the legacy tables (Discovery §4.1-bis): shared by every
/// Activities integration suite that needs to prove it survives the real drift, not just the
/// EF-model shape <see cref="SqlServerContainerFixture"/> creates by default.
/// </summary>
internal static class ActivitySchemaVariants
{
    internal const string Universal15 = "activities_variant_universal15";
    internal const string Extended16 = "activities_variant_extended16";

    internal static TheoryData<string> Variants => new() { Universal15, Extended16 };

    private const string Universal15ActivityDdl = """
        CREATE TABLE dbo.tbl_opo_negocios_actividades (
            negact_consecutivoP int IDENTITY(1,1) NOT NULL PRIMARY KEY,
            negact_neg_consecutivo int NULL,
            negact_opo_consecutivo int NULL,
            negact_per_codigo varchar(20) NOT NULL,
            negact_asesor varchar(20) NULL,
            negact_tipo char(1) NOT NULL,
            negact_fecha datetime NOT NULL,
            negact_titulo varchar(500) NULL,
            negact_descripcion varchar(MAX) NULL,
            negact_resultado char(1) NULL,
            negact_fecha_vencimiento datetime NULL,
            negact_completada bit NULL,
            negact_anulada bit NULL,
            negact_fecha_resuelto datetime NULL,
            negact_descripcion_virtual varchar(500) NULL);
        """;

    private const string Extended16ActivityDdl = """
        CREATE TABLE dbo.tbl_opo_negocios_actividades (
            negact_consecutivoP int IDENTITY(1,1) NOT NULL PRIMARY KEY,
            negact_neg_consecutivo int NULL,
            negact_opo_consecutivo int NULL,
            negact_tipo char(1) NOT NULL,
            negact_titulo varchar(500) NULL,
            negact_descripcion varchar(2000) NULL,
            negact_resultado char(1) NULL,
            negact_fecha datetime NOT NULL,
            negact_fecha_vencimiento datetime NULL,
            negact_completada bit NULL,
            negact_anulada bit NULL,
            negact_fecha_resuelto datetime NULL,
            negact_asesor varchar(20) NULL,
            negact_per_codigo varchar(20) NOT NULL,
            negact_descripcion_virtual varchar(500) NULL,
            ConsecutivoActMiG int NULL);
        """;

    // No documented drift for these three (Discovery's C1-C6 findings are scoped to
    // tbl_opo_negocios_actividades) — one shape, reused by both variants.
    private const string ForeignTablesDdl = """
        CREATE TABLE dbo.tbl_opo_negocios (
            neg_consecutivoP int NOT NULL PRIMARY KEY,
            neg_opo_consecutivo int NOT NULL,
            neg_negest_consecutivo int NOT NULL,
            neg_nombre varchar(1000) NULL);
        CREATE TABLE dbo.tbl_opo_oportunidades (
            opo_consecutivoP int NOT NULL PRIMARY KEY,
            opo_nombre varchar(1000) NULL,
            opo_estado bit NULL);
        CREATE TABLE dbo.tbl_per_personas (
            per_codigoP varchar(20) NOT NULL PRIMARY KEY,
            per_numero_identificacion varchar(20) NULL,
            per_nombres_apellidos varchar(4000) NULL);
        """;

    internal static string ConnectionString(SqlServerContainerFixture fixture, string variant) =>
        new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = variant }.ConnectionString;

    internal static ApplicationDbContext CreateContext(SqlServerContainerFixture fixture, string variant)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString(fixture, variant))
            .Options;
        return new ApplicationDbContext(options);
    }

    /// <summary>Creates the variant database and its tables on first use, and clears every row on every use.</summary>
    internal static async Task EnsureCreatedAsync(SqlServerContainerFixture fixture, string variant)
    {
        var activityDdl = variant == Universal15 ? Universal15ActivityDdl : Extended16ActivityDdl;

        var master = new SqlConnection(fixture.ConnectionString);
        await using (master.ConfigureAwait(false))
        {
            await master.OpenAsync().ConfigureAwait(false);
            await ExecuteAsync(master, $"IF DB_ID(N'{variant}') IS NULL CREATE DATABASE [{variant}];")
                .ConfigureAwait(false);
        }

        var connection = new SqlConnection(ConnectionString(fixture, variant));
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await ExecuteAsync(connection, $"""
                IF OBJECT_ID(N'dbo.tbl_opo_negocios_actividades') IS NULL
                BEGIN
                {activityDdl}
                END
                IF OBJECT_ID(N'dbo.tbl_opo_negocios') IS NULL
                BEGIN
                {ForeignTablesDdl}
                END
                DELETE FROM dbo.tbl_opo_negocios_actividades;
                DELETE FROM dbo.tbl_opo_negocios;
                DELETE FROM dbo.tbl_opo_oportunidades;
                DELETE FROM dbo.tbl_per_personas;
                """).ConfigureAwait(false);
        }
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
#pragma warning disable CA2100 // sql is always one of the const DDL strings above.
            command.CommandText = sql;
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }
}
