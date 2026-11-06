# Cambios de texto de la Fase 9b

Textos de `VoltiaMX/Strings/es-MX/Resources.resw` que cambiaron en la Fase 9b (primer uso, fecha de corte e Inicio sin pantallas vacías),
comparados contra el commit `1664f6e`. "(nueva)" marca un texto que no existía y "(borrada)" uno que ya
nadie usa. Los textos de Uso y Ubicación siguen en el resw: sus pantallas salieron del flujo, no del proyecto.

## Menú y Configuración

| Clave | Antes | Después |
|---|---|---|
| `Mas_BorradoTexto` | (nueva) | Tus datos se borraron. Cierra VoltiaMX y vuelve a abrirla para empezar de nuevo. |
| `Mas_BorradoTitulo` | (nueva) | Datos borrados |
| `Mas_BorrarBoton.Content` | (nueva) | Borrar mis datos |
| `Mas_BorrarConfirmar` | (nueva) | Borrar mis datos |
| `Mas_BorrarConfirmarTexto` | (nueva) | Se borran tus recibos, lecturas, aparatos, metas e inquilinos. Se conservan tus tarifas. Antes guardamos una copia en {0}. |
| `Mas_BorrarFallo` | (nueva) | No pudimos borrar tus datos. {0} Tu copia está en {1}. |
| `Mas_BorrarSinCopia` | (nueva) | No pudimos guardar la copia previa, así que no borramos nada. {0} |
| `Mas_BorrarTexto.Text` | (nueva) | Borra tus datos y vuelve a empezar. Antes guardamos una copia. |
| `Mas_BorrarTitulo.Text` | (nueva) | Borrar mis datos y empezar de nuevo |
| `Mas_ModoTexto.Text` | Muestra herramientas de metas, reportes, paneles solares e inquilinos. | Muestra herramientas extra: reportes, metas y paneles solares. |

## Inicio

| Clave | Antes | Después |
|---|---|---|
| `Dashboard_Aproximado.Content` | (nueva) | APROXIMADO |
| `Dashboard_EscanearMiRecibo.Content` | (nueva) | Escanear mi recibo |
| `Dashboard_EscribirCuantoPague.Content` | (nueva) | Escribir cuánto pagué |
| `Dashboard_FechaFutura` | (nueva) | La fecha de corte de tu último recibo ({0:d 'de' MMMM}) todavía no llega. Si la escribiste mal, corrígela. |
| `Dashboard_FechaFuturaIrAConfiguracion.Content` | (nueva) | Ir a Configuración |
| `Dashboard_FechaFuturaTitulo.Text` | (nueva) | REVISA TU ÚLTIMO RECIBO |
| `Dashboard_IrAMisRecibos.Content` | (nueva) | Ir a Mis recibos |
| `Dashboard_LeyendaPeriodoSinDia` | (nueva) | {0} · {1} |
| `Dashboard_ParaAfinar.Text` | (nueva) | Para afinar, anota lo que marca tu medidor en unos días. |
| `Dashboard_ProbarDemo.Content` | (nueva) | Probar con datos de ejemplo |
| `Dashboard_SiSiguesIgualTitulo` | (nueva) | SI SIGUES IGUAL QUE TU RECIBO ANTERIOR, PAGARÍAS UNOS |
| `Dashboard_SinReciboTexto.Text` | (nueva) | Con lo que pagaste en tu último recibo te decimos cuánto vas a pagar. |
| `Dashboard_SinReciboTitulo.Text` | (nueva) | AGREGA TU RECIBO |

## Mis recibos y recibo por monto

| Clave | Antes | Después |
|---|---|---|
| `ReciboMonto_Error_FechaFutura` | La fecha de corte no puede ser después de hoy. | La fecha de corte no puede ser posterior a hoy. Escribe la del recibo que ya recibiste. |

## Primer uso (onboarding)

| Clave | Antes | Después |
|---|---|---|
| `Bienvenida_ProbarDemo.Content` | (nueva) | Probar con datos de ejemplo |
| `OnboardingRecibo_Aproximado.Text` | (nueva) | Calculamos tu consumo de forma aproximada a partir de lo que pagaste. |
| `OnboardingRecibo_Ayuda.Text` | (nueva) | Con lo que pagaste en tu último recibo calculamos cuánto vas a pagar. |
| `OnboardingRecibo_ElegirArchivo.Content` | (nueva) | Elegir foto o PDF |
| `OnboardingRecibo_EscanearTexto.Text` | (nueva) | FOTO O PDF |
| `OnboardingRecibo_EscanearTitulo.Text` | (nueva) | ESCANEAR MI RECIBO |
| `OnboardingRecibo_EscribirTexto.Text` | (nueva) | Escribe el total y la fecha de corte de tu último recibo. |
| `OnboardingRecibo_EscribirTitulo.Text` | (nueva) | ESCRIBIR CUÁNTO PAGUÉ |
| `OnboardingRecibo_Fecha.Header` | (nueva) | Fecha de corte |
| `OnboardingRecibo_Fecha.PlaceholderText` | (nueva) | Elige la fecha |
| `OnboardingRecibo_Incompleto` | (nueva) | Escribe el total y la fecha de corte de tu recibo. |
| `OnboardingRecibo_Leyendo.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name` | (nueva) | Leyendo tu recibo |
| `OnboardingRecibo_Revisa` | (nueva) | Revisa que coincidan con tu recibo. |
| `OnboardingRecibo_SinRecibo.Content` | (nueva) | Todavía no tengo mi recibo |
| `OnboardingRecibo_SinTotal` | (nueva) | No pudimos leer el total de tu recibo. Escríbelo abajo. |
| `OnboardingRecibo_Titulo.Text` | (nueva) | TU RECIBO |
| `OnboardingRecibo_Total.Header` | (nueva) | Total a pagar |
| `OnboardingRecibo_Total.PlaceholderText` | (nueva) | Ej. 1020 |
| `OnboardingTarifa_Ayuda.Text` | (nueva) | Con tu tarifa sabemos cuánto cuesta lo que consumes. |
| `OnboardingTarifa_Titulo.Text` | (nueva) | TU TARIFA |
| `Resumen_ReciboTotalYFecha` | (nueva) | ${0:N0} · corte del {1:d 'de' MMMM 'de' yyyy} |
| `Resumen_SinRecibo` | (nueva) | Sin recibo todavía |
| `Resumen_TarifaPorConfirmar` | 1 · por confirmar | 1 · por confirmar en tu recibo |

## Ayuda

| Clave | Antes | Después |
|---|---|---|
| `AyudaEnlace_DondeTarifa` | (nueva) | ¿Dónde la veo en mi recibo? |
| `Ayuda_ConsumoAproximado` | (nueva) | Con tu total y los precios de tu tarifa calculamos cuánto consumiste. Es aproximado: tu recibo también trae impuestos y otros cargos. |
| `Ayuda_DondeTarifa` | (nueva) | Tu tarifa viene impresa en tu recibo de CFE. Suele ser un número, o un número con una letra, como 1 o 1C. |
| `Ayuda_Termino_ConsumoAproximado` | (nueva) | Consumo aproximado |
| `Ayuda_Termino_DondeTarifa` | (nueva) | Tu tarifa |

## Escanear recibo

| Clave | Antes | Después |
|---|---|---|
| `Escanear_Error_FechaCorteFutura` | (nueva) | La fecha de corte no puede ser posterior a hoy. Escribe la del recibo que ya recibiste. |
| `Escanear_VolverLecturas.Content` | Volver a lecturas | Volver a Mis recibos |
