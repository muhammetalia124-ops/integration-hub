using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IntegrationHub.Data.Migrations.Cache
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Cache");

            migrationBuilder.CreateTable(
                name: "CurrencyRates",
                schema: "Cache",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ForexBuying = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    ForexSelling = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyRates", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "ExternalApiCache",
                schema: "Cache",
                columns: table => new
                {
                    CacheKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalApiCache", x => x.CacheKey);
                });

            migrationBuilder.CreateTable(
                name: "PrayerTimeCache",
                schema: "Cache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DistrictId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    City = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    District = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PrayerDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Imsak = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Gunes = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Ogle = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Ikindi = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Aksam = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Yatsi = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrayerTimeCache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SharedApiData",
                schema: "Cache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DataType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecordKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DataDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsAdminManaged = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedApiData", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalApiCache_ExpiresAtUtc",
                schema: "Cache",
                table: "ExternalApiCache",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PrayerTimeCache_District_Date",
                schema: "Cache",
                table: "PrayerTimeCache",
                columns: new[] { "DistrictId", "PrayerDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedApiData_Type_Date",
                schema: "Cache",
                table: "SharedApiData",
                columns: new[] { "DataType", "DataDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedApiData_Type_Key_Date",
                schema: "Cache",
                table: "SharedApiData",
                columns: new[] { "DataType", "RecordKey", "DataDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurrencyRates",
                schema: "Cache");

            migrationBuilder.DropTable(
                name: "ExternalApiCache",
                schema: "Cache");

            migrationBuilder.DropTable(
                name: "PrayerTimeCache",
                schema: "Cache");

            migrationBuilder.DropTable(
                name: "SharedApiData",
                schema: "Cache");
        }
    }
}
