# Fase 1: Base (Core)

Lee CLAUDE.md. Vamos a implementar la FASE 1 (Base) en VoltiaMX.Core y VoltiaMX.Tests. No toques la app WinUI todavía. Usa modo plan: muéstrame el plan y espera mi aprobación antes de escribir código.

Paquetes en VoltiaMX.Core: Microsoft.EntityFrameworkCore.Sqlite y Microsoft.EntityFrameworkCore.Design (si ya están instalados desde la Fase 0, no los dupliques).

1) Crea los modelos en VoltiaMX.Core/Models/:
- Lugar.cs (Id, Nombre, Tipo [enum TipoLugar: Casa, Negocio, Propiedad], TarifaId, DiaCorte, Estado, Municipio)
- Tarifa.cs (Id, Nombre, Tipo [enum TipoTarifa: Domestica, Comercial, Dac], LimiteDacKwhMensual nullable, Vigencia)
- TarifaRango.cs (Id, TarifaId, Nombre, KwhDesde, KwhHasta nullable, PrecioPorKwh, Orden)
- Lectura.cs (Id, LugarId, Fecha, ValorKwh, Origen [enum OrigenLectura: Manual, Ocr])
- Periodo.cs (Id, LugarId, Inicio, Fin, KwhTotales, Monto)

2) Crea VoltiaMX.Core/Data/VoltiaDbContext.cs con DbSets, configuración con Fluent API, precisión decimal(18,4) en precios y montos, y una clase DbInicializador.cs que aplique migraciones y cargue datos semilla de ejemplo (tarifa 1, 1C, 1F, DAC y PDBT con rangos básico/intermedio/excedente). Marca los valores con "// EJEMPLO: verificar con tarifas vigentes de CFE".

3) Crea VoltiaMX.Core/Services/:
- ITarifaCalculator.cs y TarifaCalculator.cs: calcula el monto por rangos dado kWh y una tarifa (devuelve desglose por rango y total).
- IProyeccionService.cs y ProyeccionService.cs: dado el consumo acumulado y los días transcurridos del periodo, proyecta kWh y monto al cierre.
- IDacService.cs y DacService.cs: dado el historial de 12 meses y el límite de la tarifa, devuelve un nivel de riesgo (Bajo, Medio, Alto) y el margen en kWh.
- IPresupuestoInversoService.cs y PresupuestoInversoService.cs: dado un monto máximo y una tarifa, devuelve los kWh máximos del periodo y por día (inverso del cálculo por rangos).

4) Pruebas en VoltiaMX.Tests/ (xUnit):
- TarifaCalculatorTests: un solo rango, varios rangos, exactamente en el límite de un rango, 0 kWh, consumo muy alto.
- PresupuestoInversoServiceTests: verificar que calcular(kWh devueltos) <= presupuesto y que con 1 kWh más se pasa.
- DacServiceTests: los tres niveles de riesgo.
- ProyeccionServiceTests: proyección lineal básica y primer día del periodo.

5) Genera la migración inicial con dotnet ef (si falta la herramienta, indícame cómo instalarla).

Al terminar: dotnet build VoltiaMX.Core, dotnet test, y dame un resumen con los archivos creados y los supuestos que hiciste.
