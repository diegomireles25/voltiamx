using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltiaMX.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class TarifaEjemplo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsDeEjemplo",
                table: "Tarifas",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Las tarifas con los nombres que siembra DbInicializador traen precios de ejemplo (Fase 8, D3).
            migrationBuilder.Sql("UPDATE Tarifas SET EsDeEjemplo = 1 WHERE Nombre IN ('1', '1C', '1F', 'DAC', 'PDBT');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsDeEjemplo",
                table: "Tarifas");
        }
    }
}
