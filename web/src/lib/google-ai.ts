/**
 * Enlace al Modo IA de Google (udm=50) con la pregunta ya escrita para buscar el OEM de un repuesto. Gratis: lo abre
 * el vendedor o el cliente en su navegador. La IA sugiere; el vendedor confirma (prueba de 15 piezas, 2026-11-23).
 * Formato fijo pedido por el cliente (2026-11-24): tabla con medidas + foto de cómo luce, para poder comparar con otro
 * repuesto de las mismas medidas que sirva o se adapte.
 */
export type OemQuestion = {
  vehicle: string; // "CHEVROLET AVEO EMOTION 1.6L STD" (de la placa) o "Chevrolet Aveo" (del catálogo)
  year?: number | null;
  engine?: string | null;
  vin?: string | null;
  part: string;
};

export function googleOemQuestion({ vehicle, year, engine, vin, part }: OemQuestion): string {
  // Corta a propósito: Google pide confirmar ("¿Quieres continuar con esta búsqueda?") toda pregunta que llega por
  // enlace, y si es larga obliga a desplazarse hasta el final antes de poder aceptar. Probado 2026-11-24 con el Aveo
  // Emotion: con esta versión corta igual da OEM, medidas, equivalentes y fotos.
  return `Tabla con código OEM, medidas (largo y diámetros), equivalentes (Gates, INR, otras marcas) y foto de: ` +
    `${part.trim().slice(0, 80)}, para ${vehicle}` + (year ? ` año ${year}` : "") + (engine ? ` motor ${engine}` : "") +
    (vin ? ` chasis ${vin}` : "") + ` (Ecuador). Marca "por confirmar" lo que no sea seguro.`;
}

export const googleOemHref = (q: OemQuestion) =>
  `https://www.google.com/search?${new URLSearchParams({ udm: "50", q: googleOemQuestion(q) })}`;
