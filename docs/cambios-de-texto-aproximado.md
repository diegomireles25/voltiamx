# Cambios de texto: aproximado en lugar de unos

Textos de `VoltiaMX/Strings/es-MX/Resources.resw` comparados contra el commit `77e71d0`. Un valor suelto dice
"Aproximado $N"; dentro de una frase, "aproximadamente". "1 hora" pasa a "una hora". No cambian: "en unos días"
(`Dashboard_ParaAfinar`) ni "NECESITARÍAS UNOS" (`Solar_NecesitariasTitulo`). Ninguna clave cambia de nombre.

## Inicio

| Clave | Antes | Después |
|---|---|---|
| `Dashboard_ComparacionAbajo` | A este ritmo pagarías unos ${0:N0}. Tu recibo anterior habría sido de unos ${1:N0}, o sea unos ${2:N0} menos. | A este ritmo pagarías aproximadamente ${0:N0}. Tu recibo anterior habría sido de aproximadamente ${1:N0}, o sea aproximadamente ${2:N0} menos. |
| `Dashboard_ComparacionArriba` | A este ritmo pagarías unos ${0:N0}. Tu recibo anterior habría sido de unos ${1:N0}, o sea unos ${2:N0} más. | A este ritmo pagarías aproximadamente ${0:N0}. Tu recibo anterior habría sido de aproximadamente ${1:N0}, o sea aproximadamente ${2:N0} más. |
| `Dashboard_ComparacionIgual` | A este ritmo pagarías unos ${0:N0}. Tu recibo anterior habría sido de unos ${1:N0}: casi lo mismo. | A este ritmo pagarías aproximadamente ${0:N0}. Tu recibo anterior habría sido de aproximadamente ${1:N0}: casi lo mismo. |
| `Dashboard_MetaCumpliendo` | Vas cumpliendo tu meta del {0}%. Tu recibo debería quedar en unos ${1:N0} o menos, y vas en unos ${2:N0}. | Vas cumpliendo tu meta del {0}%. Tu recibo debería quedar en aproximadamente ${1:N0} o menos, y vas en aproximadamente ${2:N0}. |
| `Dashboard_MetaEnRiesgo` | Tu meta del {0}% está en riesgo. Te faltan unos ${1:N0} para lograrla. | Tu meta del {0}% está en riesgo. Te faltan aproximadamente ${1:N0} para lograrla. |
| `Dashboard_SiSiguesIgualTitulo` | SI SIGUES IGUAL QUE TU RECIBO ANTERIOR, PAGARÍAS UNOS | SI SIGUES IGUAL QUE TU RECIBO ANTERIOR, PAGARÍAS APROXIMADAMENTE |
| `Formato_Unos` | unos ${0:N0} | Aproximado ${0:N0} |
| `Propiedad_Detalle` | unos ${0:N0} | Aproximado ${0:N0} |
| `Sugerencia_Accion` | Usa {0} 1 hora menos al día y ahorras unos ${1:N0} al mes. | Usa {0} una hora menos al día y ahorras aproximadamente ${1:N0} al mes. |
| `Sugerencia_CombinadoDos` | Con las dos ahorrarías unos ${0:N0} al mes. | Con las dos ahorrarías aproximadamente ${0:N0} al mes. |
| `Sugerencia_CombinadoTres` | Con las tres ahorrarías unos ${0:N0} al mes. | Con las tres ahorrarías aproximadamente ${0:N0} al mes. |

## Aparatos y simulador

| Clave | Antes | Después |
|---|---|---|
| `Aparatos_CostoMes` | unos ${0:N0} al mes | Aproximado ${0:N0} al mes |
| `Aparatos_CostoPrevia` | UNOS ${0:N0} AL MES | APROXIMADO ${0:N0} AL MES |

## Paneles solares

| Clave | Antes | Después |
|---|---|---|
| `Solar_EspacioValor` | unos {0:#,##0.#} metros cuadrados | Aproximado {0:#,##0.#} metros cuadrados |

## Tu meta de ahorro

| Clave | Antes | Después |
|---|---|---|
| `Metas_CifraFalta` | TE FALTAN UNOS ${0:N0} | TE FALTAN APROXIMADAMENTE ${0:N0} |
| `Metas_LeyendaPesos` | Tu recibo debería quedar en unos ${0:N0} o menos. A este ritmo irías en unos ${1:N0}. Te faltan unos ${2:N0}. | Tu recibo debería quedar en aproximadamente ${0:N0} o menos. A este ritmo irías en aproximadamente ${1:N0}. Te faltan aproximadamente ${2:N0}. |
| `Metas_LeyendaPesosCumplida` | Tu recibo debería quedar en unos ${0:N0} o menos. A este ritmo irías en unos ${1:N0}: vas dentro de tu meta. | Tu recibo debería quedar en aproximadamente ${0:N0} o menos. A este ritmo irías en aproximadamente ${1:N0}: vas dentro de tu meta. |
| `Metas_LograrloDos` | Con las dos ahorrarías unos ${0:N0} por recibo. | Con las dos ahorrarías aproximadamente ${0:N0} por recibo. |
| `Metas_LograrloTres` | Con las tres ahorrarías unos ${0:N0} por recibo. | Con las tres ahorrarías aproximadamente ${0:N0} por recibo. |
| `Metas_LograrloUna` | Con esta ahorrarías unos ${0:N0} por recibo. | Con esta ahorrarías aproximadamente ${0:N0} por recibo. |

## Mis recibos y recibo por monto

| Clave | Antes | Después |
|---|---|---|
| `ReciboMonto_Confirmacion` | Con ese monto, tu consumo habría sido de unos {0:N0} kWh (aproximado). | Con ese monto, tu consumo habría sido de aproximadamente {0:N0} kWh. |

## Primer uso (onboarding)

| Clave | Antes | Después |
|---|---|---|
| `Resumen_ReciboPorMonto` | Pagaste ${0:N0}: unos {1:N0} kWh (aproximado) | Pagaste ${0:N0}: aproximadamente {1:N0} kWh. |
