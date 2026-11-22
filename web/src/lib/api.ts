import "server-only";

/**
 * Cliente de la API .NET. Solo se usa en el servidor (Server Components):
 * el navegador nunca habla directo con la API ni con Meilisearch.
 */
const API_URL = process.env.API_URL ?? "http://localhost:8080";

// Los datos cambian como mucho cada 60 min (sincronización); 5 min de caché es suficiente.
const REVALIDATE_SECONDS = 300;

export type CatalogHit = {
  slug: string;
  title: string;
  /** Vacío en fichas sin OEM: entonces se muestra codes[0]. */
  oem: string;
  codes: string[];
  brands: string[];
  models: string[];
  category: string | null;
  subcategory: string | null;
  available: boolean;
  isVch: boolean;
  abcClass: string | null;
  pvpMin: number;
  pvpMax: number;
  mayorMin: number | null;
  image: string | null;
  /** Unidades totales de todos los locales (0 = bajo pedido). */
  stock: number;
  /** "Marca|Modelo|desde|hasta" (años leídos de la descripción). */
  yearRanges?: string[];
};

export type CatalogPage = {
  items: CatalogHit[];
  total: number;
  page: number;
  totalPages: number;
  relaxed: boolean;
  facets: { brands?: Record<string, number>; categories?: Record<string, number> };
};

export type Variant = { code: string; isVch: boolean; pvp: number; precioMayor: number | null; available: boolean; stock: number };
export type Vehicle = { brand: string; model: string | null };
/** source: "descripcion" (inventario del cliente) u "hoja" (compatibilidades verificadas). */
export type VehicleYears = { brand: string; model: string; from: number; to: number; source: string; version?: string; engine?: string };

export type Item = {
  slug: string;
  oem: string;
  oemAliases: string[];
  title: string;
  description: string;
  category: string | null;
  subcategory: string | null;
  isVch: boolean;
  pvpMin: number;
  pvpMax: number;
  mayorMin: number | null;
  available: boolean;
  abcClass: string | null;
  image: string | null;
  updatedAt: string;
  variants: Variant[];
  vehicles: Vehicle[];
  stock: number;
  years?: VehicleYears[];
};

export type Brand = { brand: string; count: number; models: { name: string; count: number; engines?: string[] }[] };
export type Category = { name: string; count: number; subcategories: { name: string; count: number }[] };

export type CatalogQuery = {
  q?: string;
  brand?: string;
  model?: string;
  category?: string;
  subcategory?: string;
  available?: boolean;
  /** true = solo importación directa (Importadora VCH); false = solo los demás. */
  vch?: boolean;
  page?: number;
  pageSize?: number;
  /** Solo con marca y modelo: oculta lo que sabemos que es de otros años. */
  year?: number;
  /** Cilindraje ("1.4"), solo con marca y modelo: oculta lo que sabemos que es de otro motor. */
  engine?: string;
};

async function get<T>(path: string, revalidate = REVALIDATE_SECONDS): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, { next: { revalidate } });
  if (!res.ok) throw new Error(`API ${res.status} en ${path}`);
  return (await res.json()) as T;
}

export function searchCatalog(query: CatalogQuery): Promise<CatalogPage> {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== "") params.set(key, String(value));
  }
  return get<CatalogPage>(`/api/catalog?${params}`);
}

export async function getItem(slug: string): Promise<Item | null> {
  const res = await fetch(`${API_URL}/api/catalog/${encodeURIComponent(slug)}`, {
    next: { revalidate: REVALIDATE_SECONDS },
  });
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`API ${res.status} en ficha ${slug}`);
  return (await res.json()) as Item;
}

export const getVehicles = () => get<Brand[]>("/api/vehicles", 3600);

/** Opciones de la barra "Mi garaje" y del garaje: marcas, modelos y cilindrajes por modelo. */
export const vehicleOptions = (brands: Brand[]) =>
  brands.map((b) => ({
    brand: b.brand,
    models: b.models.map((m) => m.name),
    engines: Object.fromEntries(b.models.filter((m) => m.engines?.length).map((m) => [m.name, m.engines!])),
  }));
export const getCategories = () => get<Category[]>("/api/categories", 3600);
export const getSlugs = () => get<{ slug: string; updatedAt: string }[]>("/api/catalog/slugs", 3600);
