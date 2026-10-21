using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltiaMX.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class NombreTarifa40 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El largo del nombre de la tarifa pasa de 20 a 40: en SQLite no cambia el esquema
            // (TEXT no tiene largo), solo el modelo de EF.

            // HEURÍSTICA: hasta ahora el periodo que se captura del recibo en el onboarding guardaba el
            // monto real del recibo en Monto, no en MontoRecibo. Para que "Recalcular mis periodos" no
            // lo pierda, se copia a MontoRecibo en los periodos de lugares reales que no tienen lectura
            // de corte en su fecha Fin: LecturaService y la demo siempre dejan esa lectura, así que esos
            // periodos solo pueden venir del recibo. Puede equivocarse si alguien borró a mano la
            // lectura de corte de un periodo cerrado.
            migrationBuilder.Sql("""
                UPDATE Periodos
                SET MontoRecibo = Monto
                WHERE MontoRecibo IS NULL
                  AND LugarId IN (SELECT Id FROM Lugares WHERE EsDemo = 0)
                  AND NOT EXISTS (
                      SELECT 1 FROM Lecturas
                      WHERE Lecturas.LugarId = Periodos.LugarId AND Lecturas.Fecha = Periodos.Fin);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La copia a MontoRecibo no se deshace: no se puede saber qué valores ya existían.
        }
    }
}
