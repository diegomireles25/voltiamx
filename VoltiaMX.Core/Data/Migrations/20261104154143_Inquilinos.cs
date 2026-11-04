using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltiaMX.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inquilinos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inquilinos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LugarId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false, collation: "NOCASE"),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaAlta = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inquilinos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inquilinos_Lugares_LugarId",
                        column: x => x.LugarId,
                        principalTable: "Lugares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Repartos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LugarId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodoInicio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PeriodoFin = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TotalRepartido = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    OrigenTotal = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Modo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    KwhPrincipal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repartos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repartos_Lugares_LugarId",
                        column: x => x.LugarId,
                        principalTable: "Lugares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepartoInquilinos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepartoId = table.Column<int>(type: "INTEGER", nullable: false),
                    InquilinoId = table.Column<int>(type: "INTEGER", nullable: true),
                    NombreInquilino = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    LecturaAnterior = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    LecturaActual = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Kwh = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Cuota = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepartoInquilinos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepartoInquilinos_Inquilinos_InquilinoId",
                        column: x => x.InquilinoId,
                        principalTable: "Inquilinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RepartoInquilinos_Repartos_RepartoId",
                        column: x => x.RepartoId,
                        principalTable: "Repartos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inquilinos_LugarId_Nombre",
                table: "Inquilinos",
                columns: new[] { "LugarId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepartoInquilinos_InquilinoId",
                table: "RepartoInquilinos",
                column: "InquilinoId");

            migrationBuilder.CreateIndex(
                name: "IX_RepartoInquilinos_RepartoId",
                table: "RepartoInquilinos",
                column: "RepartoId");

            migrationBuilder.CreateIndex(
                name: "IX_Repartos_LugarId_PeriodoInicio_PeriodoFin",
                table: "Repartos",
                columns: new[] { "LugarId", "PeriodoInicio", "PeriodoFin" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepartoInquilinos");

            migrationBuilder.DropTable(
                name: "Inquilinos");

            migrationBuilder.DropTable(
                name: "Repartos");
        }
    }
}
