/**
 * Logos de las marcas de vehículo para "Busca por marca" (pedido del cliente 2026-11-22, como Mansuera).
 * Archivos en public/marcas/. Fuentes: Simple Icons (CC0, coloreados con el color de cada marca) y
 * Wikimedia Commons (dominio público: Chery, GWM). Daewoo y Datsun los consiguió el cliente (WebP).
 * Las marcas sin logo se muestran con su nombre.
 */
const logos: Record<string, string> = {
  chevrolet: "chevrolet",
  hyundai: "hyundai",
  nissan: "nissan",
  kia: "kia",
  mazda: "mazda",
  toyota: "toyota",
  suzuki: "suzuki",
  renault: "renault",
  ford: "ford",
  daewoo: "daewoo.webp",
  mitsubishi: "mitsubishi",
  volkswagen: "volkswagen",
  fiat: "fiat",
  chery: "chery",
  peugeot: "peugeot",
  "great wall": "greatwall",
  datsun: "datsun.webp",
  "mercedes-benz": "mercedes",
  honda: "honda",
  skoda: "skoda",
  citroen: "citroen",
};

const fold = (s: string) => s.normalize("NFD").replace(/\p{M}/gu, "").trim().toLowerCase();

export function brandLogo(brand: string): string | null {
  const file = logos[fold(brand)];
  return file ? `/marcas/${file.includes(".") ? file : `${file}.svg`}` : null;
}

/** Texto corto debajo de cada categoría en el inicio (como Mansuera: "Plumas, Químicos, Repuestos"). */
const examples: Record<string, string> = {
  "Mangueras": "Radiador, calefacción, depurador",
  "Bases de motor y caja": "Bases de motor, soportes de caja",
  "Bases y topes de amortiguador": "Bases de amortiguador, topes, alzas",
  "Soportes de cardán y cabina": "Soportes de cardán, bases de cabina",
  "Bases y soportes": "Soportes de amortiguador y de radiador",
  "Bujes y bocines": "Bujes de mesa, bocines de plato y de hoja",
  "Guardapolvos": "Guardapolvos de eje, fuelles de dirección",
  "Cauchos": "Cauchos de barra, de caja, de cremallera",
  "Bandas": "Bandas lisas, dentadas, de distribución",
  "Filtros": "Filtros de aire, aceite y combustible",
  "Frenos": "Pastillas, antirruidos",
  "Kits de reparación": "Kits de pernos, de mordaza",
  "Tubos y enfriamiento": "Tubos de calefacción, de agua",
  "Empaques, capuchones y tapas": "Capuchones de bobina, empaques, tapas",
  "Tornillería y abrazaderas": "Abrazaderas, pernos de barra",
  "Lubricantes y químicos": "Refrigerantes, silicones, líquidos",
  "Perfiles y burletes": "Cauchos de puerta, perfiles",
  "Válvulas": "Válvulas PCV, de calefacción",
  "Rodamientos": "Rulimanes de amortiguador y de centro",
  "Suspensión y dirección": "Mesas, barras estabilizadoras, links",
  "Cables, pedales y palancas": "Cables de acelerador, pedales",
  "Eléctricos e iluminación": "Cables de bujía, velocímetro",
};

export const categoryExamples = (name: string) => examples[name] ?? null;
