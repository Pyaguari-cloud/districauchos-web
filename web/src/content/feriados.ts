/**
 * Feriados nacionales de Ecuador en los que NO se despacha (cliente, 2026-11-19: "domingos y feriados no se
 * despacha, se despacha al día laborable más cercano").
 *
 * ACTUALIZAR CADA AÑO con el decreto oficial del Ministerio de Turismo: el Gobierno mueve algunos feriados
 * (al lunes o al viernes) y a veces agrega días de descanso. Fechas ya movidas, formato AAAA-MM-DD.
 * 2027 = cálculo según la regla de traslado; confirmar cuando salga el calendario oficial.
 */
export const FERIADOS: readonly string[] = [
  // 2026 (lo que queda del año)
  "2026-12-25", // Navidad
  // 2027
  "2027-01-01", // Año Nuevo
  "2027-02-08", "2027-02-09", // Carnaval
  "2027-03-26", // Viernes Santo
  "2027-04-30", // Día del Trabajo (1 de mayo, sábado → viernes)
  "2027-05-24", // Batalla de Pichincha
  "2027-08-09", // Primer Grito de Independencia (10 de agosto, martes → lunes)
  "2027-10-08", // Independencia de Guayaquil (9 de octubre, sábado → viernes)
  "2027-11-02", "2027-11-03", // Difuntos e Independencia de Cuenca
  "2027-12-24", // Navidad (25 de diciembre, sábado → viernes)
];
