// "$7.00" (punto decimal), igual que el mensaje de WhatsApp del brief §6.7 y las etiquetas de tienda.
const usd = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", minimumFractionDigits: 2 });

export const money = (value: number) => usd.format(value);

export function priceRange(min: number, max: number) {
  return min === max ? money(min) : `${money(min)} – ${money(max)}`;
}

/**
 * Título para mostrar (propuesta de mejora 5): quita "(IMPORTADORA VCH)" y variantes mal escritas.
 * Solo cambia cómo se ve en la página; la hoja del cliente no se toca.
 */
export function displayTitle(title: string) {
  return title
    .replace(/\(?\s*(?:IMPORTADORA|MPORTADORA)\s*VCH\s*\)?/gi, "")
    .replace(/\s{2,}/g, " ")
    .replace(/[\s,\-–/]+$/, "")
    .trim();
}

/** Categorías que en la hoja no son categorías reales (se ocultan del menú). */
const PSEUDO_CATEGORIES = new Set(["PRODUCTO IMPORTADO", "PRODUCTO NACIONAL"]);
export const isRealCategory = (name: string) => !PSEUDO_CATEGORIES.has(name.trim().toUpperCase());

/** "MANGUERAS CODO" → "Mangueras codo" (para menús y migas de pan). */
export function sentenceCase(text: string) {
  const lower = text.toLocaleLowerCase("es-EC");
  return lower.charAt(0).toLocaleUpperCase("es-EC") + lower.slice(1);
}

export function vehicleLabel(brand: string, model: string | null) {
  return model ? `${brand} ${model}` : brand;
}
