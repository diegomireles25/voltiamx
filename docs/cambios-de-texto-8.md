# Cambios de texto de la Fase 8

Textos de `VoltiaMX/Strings/es-MX/Resources.resw` que cambiaron en la Fase 8 (pesos y acciones en lugar de kWh),
comparados contra el commit `07d9d19`. "(nueva)" marca un texto que no existía y "(borrada)" uno que ya
nadie usa. Dos claves solo cambiaron de nombre porque ahora las pone el ViewModel: `Aparatos_SinVistaPrevia`
(antes `.Text`) y `Sugerencia_Depende.Text`.

## Inicio

| Clave | Antes | Después |
|---|---|---|
| `Dashboard_ComparacionAbajo` | A este ritmo vas {0:0} % más bajo que tu recibo anterior | A este ritmo pagarías unos ${0:N0}. Tu recibo anterior habría sido de unos ${1:N0}, o sea unos ${2:N0} menos. |
| `Dashboard_ComparacionArriba` | A este ritmo vas {0:0} % más alto que tu recibo anterior | A este ritmo pagarías unos ${0:N0}. Tu recibo anterior habría sido de unos ${1:N0}, o sea unos ${2:N0} más. |
| `Dashboard_ComparacionCifraAbajo` | −{0:0} % | −${0:N0} |
| `Dashboard_ComparacionCifraArriba` | +{0:0} % | +${0:N0} |
| `Dashboard_ComparacionIgual` | A este ritmo vas parecido a tu recibo anterior | A este ritmo pagarías unos ${0:N0}. Tu recibo anterior habría sido de unos ${1:N0}: casi lo mismo. |
| `Dashboard_ConPreciosDeEjemplo.Text` | (nueva) | CON PRECIOS DE EJEMPLO |
| `Dashboard_DacLimite` | de {0:N0} kWh al mes | (borrada) |
| `Dashboard_DiaDe` | (nueva) | DÍA {0} DE {1} |
| `Dashboard_FaltaUnDia` | (nueva) | FALTA 1 DÍA PARA TU CORTE |
| `Dashboard_FaltanDias` | (nueva) | FALTAN {0} DÍAS PARA TU CORTE |
| `Dashboard_HistorialBimestre` | CONSUMO POR BIMESTRE | (borrada) |
| `Dashboard_HistorialMes` | CONSUMO POR MES | (borrada) |
| `Dashboard_KwhAlCierre` | ~{0:N0} A ESTE RITMO | (borrada) |
| `Dashboard_KwhHastaHoy` | {0:N0} KWH HASTA HOY | (borrada) |
| `Dashboard_MetaCumpliendo` | Vas cumpliendo tu meta del {0}%: puedes gastar {1:N1} kWh al día. | Vas cumpliendo tu meta del {0}%. Tu recibo debería quedar en unos ${1:N0} o menos, y vas en unos ${2:N0}. |
| `Dashboard_MetaEnRiesgo` | Tu meta del {0}% está en riesgo: para lograrla, gasta máximo {1:N1} kWh al día. | Tu meta del {0}% está en riesgo. Te faltan unos ${1:N0} para lograrla. |
| `Dashboard_NoSumanRecibo.Text` | (nueva) | Estas cifras no suman tu recibo: lo primero que consumes cuesta menos. |
| `Dashboard_ParaAhorrar.Text` | (nueva) | PARA AHORRAR |
| `Formato_Entero` | {0:N0} | (borrada) |
| `Formato_Unos` | (nueva) | unos ${0:N0} |
| `Propiedad_Detalle` | ${0:N0} · {1:N0} kWh | unos ${0:N0} |
| `Sugerencia_Accion` | (nueva) | Usa {0} 1 hora menos al día y ahorras unos ${1:N0} al mes. |
| `Sugerencia_CombinadoDos` | (nueva) | Con las dos ahorrarías unos ${0:N0} al mes. |
| `Sugerencia_CombinadoTres` | (nueva) | Con las tres ahorrarías unos ${0:N0} al mes. |
| `Sugerencia_Depende.Text` | (nueva) | Depende de lo que registraste en Aparatos. |

## Precios de ejemplo

| Clave | Antes | Después |
|---|---|---|
| `PreciosEjemplo_Aviso.Text` | (nueva) | Estos precios son de ejemplo. Escribe los de tu recibo en Tarifas para ver cifras reales. |
| `PreciosEjemplo_Etiqueta.Content` | (nueva) | PRECIOS DE EJEMPLO |
| `PreciosEjemplo_IrATarifas.Content` | (nueva) | Ir a Tarifas |

## Aparatos y simulador

| Clave | Antes | Después |
|---|---|---|
| `Aparatos_AsiSeVeriaTexto.Text` | Consumo aproximado con estos datos. No es lo que mide tu medidor. | Lo que te costaría al mes con estos datos, antes de impuestos. |
| `Aparatos_CostoMes` | (nueva) | unos ${0:N0} al mes |
| `Aparatos_CostoPrevia` | (nueva) | UNOS ${0:N0} AL MES |
| `Aparatos_KwhMes` | {0:N1} kWh/mes | (borrada) |
| `Aparatos_KwhPrevia` | {0:N1} KWH AL MES | (borrada) |
| `Aparatos_NoSumanRecibo` | (nueva) | Estas cifras no suman tu recibo: los primeros kWh cuestan menos. |
| `Aparatos_PreviaSinPesos` | (nueva) | Registra un recibo o dos lecturas para ver cuánto te costaría. |
| `Aparatos_SinVistaPrevia` | (nueva) | Completa los datos para ver cuánto te costaría al mes. |
| `Aparatos_SinVistaPrevia.Text` | Completa los datos para ver cuánto consumiría al mes. | (borrada) |
| `Aparatos_Subtitulo.Text` | Calcula cuánto consume cada aparato al mes con su potencia y su uso. | Calcula cuánto te cuesta cada aparato al mes con su potencia y su uso. |
| `Ranking_Medido` | Porcentaje de lo que mide tu medidor: {0:N0} kWh al mes. | (borrada) |
| `Ranking_SinConsumo` | Aún no medimos tu consumo. Con dos lecturas o un recibo anterior verás porcentajes. Mientras, ves los kWh al mes aproximados. | Aún no medimos tu consumo. Registra un recibo o dos lecturas para ver cuánto cuesta cada aparato. |
| `Ranking_SinConsumoCorto` | Aún no medimos tu consumo: son kWh al mes aproximados. | Registra un recibo o dos lecturas para ver cuánto cuesta cada aparato. |
| `Simulador_KwhMes` | (nueva) | {0:N1} kWh/mes |

## Tu meta de ahorro

| Clave | Antes | Después |
|---|---|---|
| `Metas_CifraDentro` | (nueva) | VAS DENTRO DE TU META |
| `Metas_CifraFalta` | (nueva) | TE FALTAN UNOS ${0:N0} |
| `Metas_CifraSuperada` | (nueva) | YA PASASTE EL OBJETIVO |
| `Metas_ComoLograrlo.Text` | (nueva) | CÓMO LOGRARLO |
| `Metas_Explicacion.Text` | Gasta menos que en tu recibo anterior. Todo en kWh, con tus lecturas. | Paga menos que en tu recibo anterior. Lo calculamos con tus lecturas y tu tarifa. |
| `Metas_FilaPesos` | El objetivo en tu recibo, antes de impuestos | Lo que buscas ahorrar en este recibo |
| `Metas_KwhPorDia` | {0:N1} KWH AL DÍA | (borrada) |
| `Metas_LeyendaKwhPorDia` | Es lo que puedes gastar al día de aquí al corte para lograr tu meta. | (borrada) |
| `Metas_LeyendaPesos` | (nueva) | Tu recibo debería quedar en unos ${0:N0} o menos. A este ritmo irías en unos ${1:N0}. Te faltan unos ${2:N0}. |
| `Metas_LeyendaPesosCumplida` | (nueva) | Tu recibo debería quedar en unos ${0:N0} o menos. A este ritmo irías en unos ${1:N0}: vas dentro de tu meta. |
| `Metas_LograrloCubre` | (nueva) | Eso cubre lo que te falta. |
| `Metas_LograrloDos` | (nueva) | Con las dos ahorrarías unos ${0:N0} por recibo. |
| `Metas_LograrloParte` | (nueva) | Eso cubre una parte de lo que te falta. |
| `Metas_LograrloTres` | (nueva) | Con las tres ahorrarías unos ${0:N0} por recibo. |
| `Metas_LograrloUna` | (nueva) | Con esta ahorrarías unos ${0:N0} por recibo. |
| `Metas_ProgresoTexto` | {0:N0} DE {1:N0} KWH DEL OBJETIVO | LLEVAS {0:0} % DEL OBJETIVO |

## Avisos

| Clave | Antes | Después |
|---|---|---|
| `Aviso_Corte` | Tu recibo cierra en {0} días. | (borrada) |

## Recibo por monto

| Clave | Antes | Después |
|---|---|---|
| `Lecturas_AgregarPorMonto.Content` | (nueva) | Agregar un recibo anterior por su monto |
| `ReciboMonto_Cancelar.Content` | (nueva) | Cancelar |
| `ReciboMonto_Cargos.Description` | (nueva) | 16 % es un valor de ejemplo. Revisa los cargos de tu recibo. |
| `ReciboMonto_Cargos.Header` | (nueva) | Impuestos y otros cargos (%) |
| `ReciboMonto_Confirmacion` | (nueva) | Con ese monto, tu consumo habría sido de unos {0:N0} kWh (aproximado). |
| `ReciboMonto_ConfirmacionTitulo.Text` | (nueva) | ASÍ QUEDARÍA |
| `ReciboMonto_Error_CargosFueraDeRango` | (nueva) | Los impuestos y cargos van de 0 % a 40 %. |
| `ReciboMonto_Error_FechaFutura` | (nueva) | La fecha de corte no puede ser después de hoy. |
| `ReciboMonto_Error_LugarNoExiste` | (nueva) | No encontramos tu lugar. Termina de configurarlo primero. |
| `ReciboMonto_Error_MontoNoAlcanza` | (nueva) | Con ese monto no sale consumo: no alcanza ni para el cargo fijo de tu tarifa. |
| `ReciboMonto_Error_TotalFueraDeRango` | (nueva) | Escribe un total mayor a $0 y de hasta $1,000,000. |
| `ReciboMonto_Error_Traslape` | (nueva) | Ya tienes un recibo en esas fechas. Elige otra fecha de corte. |
| `ReciboMonto_Explicacion.Text` | (nueva) | Si no tienes las lecturas de tu medidor, escribe cuánto pagaste. Calculamos más o menos cuánto consumiste. |
| `ReciboMonto_Fecha.Header` | (nueva) | Día de corte del recibo |
| `ReciboMonto_Fechas` | (nueva) | Del {0:d 'de' MMMM} al {1:d 'de' MMMM 'de' yyyy} |
| `ReciboMonto_Guardar.Content` | (nueva) | Guardar recibo |
| `ReciboMonto_Honesto.Text` | (nueva) | Tu recibo incluye impuestos y cargos que VoltiaMX no calcula; si tienes las lecturas de tu medidor, úsalas. |
| `ReciboMonto_Incompleto.Text` | (nueva) | Escribe el total y el día de corte para continuar. |
| `ReciboMonto_SinConfirmacion.Text` | (nueva) | Escribe el total de tu recibo para ver cuánto consumiste. |
| `ReciboMonto_SinLugar.Text` | (nueva) | Primero termina de configurar tu lugar para agregar recibos. |
| `ReciboMonto_Titulo.Text` | (nueva) | UN RECIBO ANTERIOR POR SU MONTO |
| `ReciboMonto_Total.Header` | (nueva) | Total a pagar del recibo |
| `ReciboMonto_Volver.Content` | (nueva) | Volver a Lecturas |
| `Recibo_PorMonto_Descripcion.Text` | (nueva) | Escribe el total de tu último recibo y calculamos tu consumo. |
| `Recibo_PorMonto_Titulo.Text` | (nueva) | SOLO SÉ CUÁNTO PAGUÉ |
| `Resumen_ReciboPorMonto` | (nueva) | Pagaste ${0:N0}: unos {1:N0} kWh (aproximado) |

## Reportes (PDF y Excel)

| Clave | Antes | Después |
|---|---|---|
| `Reporte_PreciosDeEjemplo` | (nueva) | Estos precios son de ejemplo. Escribe los de tu recibo en Tarifas para ver cifras reales. |
