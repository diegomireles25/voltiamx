using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltiaMX.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Aparatos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Aparatos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LugarId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    Watts = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    HorasPorDia = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    DiasPorSemana = table.Column<int>(type: "INTEGER", nullable: false),
                    CicloDeTrabajo = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aparatos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aparatos_Lugares_LugarId",
                        column: x => x.LugarId,
                        principalTable: "Lugares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aparatos_LugarId",
                table: "Aparatos",
                column: "LugarId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aparatos");
        }
    }
}
