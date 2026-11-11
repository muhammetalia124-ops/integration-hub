using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IntegrationHub.Data.Migrations.Activity
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Activity");

            migrationBuilder.CreateTable(
                name: "UserActivities",
                schema: "Activity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ServiceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SubCategory = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InputLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    InputValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResultLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ResultValue = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_Lookup",
                schema: "Activity",
                table: "UserActivities",
                columns: new[] { "UserId", "ServiceType", "SubCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_Timestamp",
                schema: "Activity",
                table: "UserActivities",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserActivities",
                schema: "Activity");
        }
    }
}
