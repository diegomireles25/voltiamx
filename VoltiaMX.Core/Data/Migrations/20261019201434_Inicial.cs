using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltiaMX.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tarifas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LimiteDacKwhMensual = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    CargoFijoMensual = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Vigencia = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tarifas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lugares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TarifaId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiaCorte = table.Column<int>(type: "INTEGER", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Municipio = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lugares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lugares_Tarifas_TarifaId",
                        column: x => x.TarifaId,
                        principalTable: "Tarifas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TarifaRangos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TarifaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    KwhDesde = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    KwhHasta = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    PrecioPorKwh = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TarifaRangos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TarifaRangos_Tarifas_TarifaId",
                        column: x => x.TarifaId,
                        principalTable: "Tarifas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Lecturas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LugarId = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ValorKwh = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Origen = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lecturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lecturas_Lugares_LugarId",
                        column: x => x.LugarId,
                        principalTable: "Lugares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Periodos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LugarId = table.Column<int>(type: "INTEGER", nullable: false),
                    Inicio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Fin = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    KwhTotales = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Monto = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Periodos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Periodos_Lugares_LugarId",
                        column: x => x.LugarId,
                        principalTable: "Lugares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lecturas_LugarId_Fecha",
                table: "Lecturas",
                columns: new[] { "LugarId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Lugares_TarifaId",
                table: "Lugares",
                column: "TarifaId");

            migrationBuilder.CreateIndex(
                name: "IX_Periodos_LugarId_Inicio",
                table: "Periodos",
                columns: new[] { "LugarId", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_TarifaRangos_TarifaId_Orden",
                table: "TarifaRangos",
                columns: new[] { "TarifaId", "Orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tarifas_Nombre",
                table: "Tarifas",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Lecturas");

            migrationBuilder.DropTable(
                name: "Periodos");

            migrationBuilder.DropTable(
                name: "TarifaRangos");

            migrationBuilder.DropTable(
                name: "Lugares");

            migrationBuilder.DropTable(
                name: "Tarifas");
        }
    }
}
