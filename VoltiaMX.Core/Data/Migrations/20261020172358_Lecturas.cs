using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltiaMX.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Lecturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Estimado",
                table: "Periodos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoRecibo",
                table: "Periodos",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: true);

            // Los lugares que ya existen quedan bimestrales, igual que hasta ahora.
            migrationBuilder.AddColumn<int>(
                name: "MesesPorPeriodo",
                table: "Lugares",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<bool>(
                name: "EsEstimada",
                table: "Lecturas",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Estimado",
                table: "Periodos");

            migrationBuilder.DropColumn(
                name: "MontoRecibo",
                table: "Periodos");

            migrationBuilder.DropColumn(
                name: "MesesPorPeriodo",
                table: "Lugares");

            migrationBuilder.DropColumn(
                name: "EsEstimada",
                table: "Lecturas");
        }
    }
}
