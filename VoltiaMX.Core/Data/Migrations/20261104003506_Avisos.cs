using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltiaMX.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Avisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvisosEnviados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LugarId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Clave = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    FechaEnvio = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisosEnviados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvisosEnviados_Lugares_LugarId",
                        column: x => x.LugarId,
                        principalTable: "Lugares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvisosEnviados_LugarId_Tipo_Clave",
                table: "AvisosEnviados",
                columns: new[] { "LugarId", "Tipo", "Clave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvisosEnviados");
        }
    }
}
