"use client";

import { createLocalStore } from "./local-store";

/**
 * "Mi garaje" (como el de Amazon): hasta 5 vehículos guardados y uno activo.
 * El activo filtra las búsquedas, va en la cotización y marca la compatibilidad en la ficha.
 * Año y cilindraje son opcionales: ocultan solo los repuestos que sabemos que son de otro año o motor
 * (lo que no tiene ese dato sigue apareciendo) y viajan en la cotización y en el pedido para que el vendedor confirme.
 */
export type GarageVehicle = { id: string; brand: string; model: string | null; year: number | null; engine: string | null };
export type MyVehicle = { brand: string; model: string | null; year?: number | null; engine?: string | null } | null;
type Garage = { vehicles: GarageVehicle[]; activeId: string | null };

export const MAX_GARAGE = 5;
export const MIN_YEAR = 1970;
export const MAX_YEAR = new Date().getFullYear() + 1;
/** Años para elegir, del más nuevo al más viejo. */
export const YEARS = Array.from({ length: MAX_YEAR - MIN_YEAR + 1 }, (_, i) => MAX_YEAR - i);

const EMPTY: Garage = { vehicles: [], activeId: null };

const cleanYear = (y: unknown) => (typeof y === "number" && Number.isInteger(y) && y >= MIN_YEAR && y <= MAX_YEAR ? y : null);
const cleanEngine = (e: unknown) => (typeof e === "string" && /^\d\.\d$/.test(e) ? e : null);
// Sin cilindraje el id es igual al de antes: los garajes ya guardados siguen funcionando.
const idOf = (brand: string, model: string | null, year: number | null, engine: string | null = null) =>
  `${brand}|${model ?? ""}|${year ?? ""}${engine ? `|${engine}` : ""}`;

// Misma clave que antes: los garajes ya guardados (sin año) siguen funcionando.
const store = createLocalStore<Garage>("dc.garage.v1", EMPTY, (v) => {
  const g = v as Partial<Garage> | null;
  const raw = Array.isArray(g?.vehicles) ? g!.vehicles : [];
  const vehicles = raw
    .filter((x): x is GarageVehicle => !!x && typeof x.brand === "string" && x.brand.length > 0)
    .map((x) => {
      const model = typeof x.model === "string" && x.model ? x.model : null;
      const year = cleanYear(x.year);
      const engine = model ? cleanEngine(x.engine) : null;
      return { id: idOf(x.brand, model, year, engine), brand: x.brand, model, year, engine };
    })
    .slice(0, MAX_GARAGE);
  // Garajes viejos guardaban un id sin el año: se ubica el vehículo guardado y se recalcula su id.
  const a = raw.find((x) => !!x && (x as GarageVehicle).id === g?.activeId) as GarageVehicle | undefined;
  const aId = a && typeof a.brand === "string" ? idOf(a.brand, a.model || null, cleanYear(a.year), a.model ? cleanEngine(a.engine) : null) : null;
  const activeId = aId && vehicles.some((x) => x.id === aId) ? aId : null;
  return { vehicles, activeId };
});

export const useGarage = store.use;

/** Vehículo activo (o null = sin filtro). */
export function useMyVehicle(): MyVehicle {
  const g = store.use();
  const v = g.vehicles.find((x) => x.id === g.activeId);
  return v ? { brand: v.brand, model: v.model, year: v.year, engine: v.engine } : null;
}

export const garage = {
  /** Agrega (o reutiliza si ya existe) y lo deja activo. Si está lleno, reemplaza el más antiguo. */
  addAndActivate(brand: string, model: string | null, year: number | null = null, engine: string | null = null) {
    const y = cleanYear(year);
    const e = model ? cleanEngine(engine) : null;
    const id = idOf(brand, model, y, e);
    store.set((g) => {
      const rest = g.vehicles.filter((x) => x.id !== id);
      const vehicles = [{ id, brand, model, year: y, engine: e }, ...rest].slice(0, MAX_GARAGE);
      return { vehicles, activeId: id };
    });
  },
  /** Cambia el año de un vehículo guardado (sigue activo si lo estaba). */
  setYear(id: string, year: number | null) {
    store.set((g) => {
      const v = g.vehicles.find((x) => x.id === id);
      if (!v) return g;
      const y = cleanYear(year);
      const newId = idOf(v.brand, v.model, y, v.engine);
      const vehicles = g.vehicles
        .filter((x) => x.id !== newId || x.id === id)
        .map((x) => (x.id === id ? { ...x, id: newId, year: y } : x));
      return { vehicles, activeId: g.activeId === id ? newId : g.activeId };
    });
  },
  activate(id: string | null) {
    store.set((g) => ({ ...g, activeId: id && g.vehicles.some((x) => x.id === id) ? id : null }));
  },
  remove(id: string) {
    store.set((g) => ({ vehicles: g.vehicles.filter((x) => x.id !== id), activeId: g.activeId === id ? null : g.activeId }));
  },
  /** Vehículos guardados en este navegador (para sincronizar con la cuenta). */
  vehicles: () => store.get().vehicles,
  /**
   * Junta los vehículos de la cuenta con los de este navegador (primero los de la cuenta, sin repetir, hasta 5)
   * y deja activo el que estaba. Pasa todo por la misma limpieza que lo leído de localStorage.
   */
  merge(fromAccount: unknown[]) {
    store.set((g) => {
      const seen = new Set<string>();
      const vehicles: GarageVehicle[] = [];
      for (const x of [...fromAccount, ...g.vehicles]) {
        const v = x as Partial<GarageVehicle> | null;
        if (!v || typeof v.brand !== "string" || !v.brand) continue;
        const model = typeof v.model === "string" && v.model ? v.model : null;
        const year = cleanYear(v.year);
        const engine = model ? cleanEngine(v.engine) : null;
        const id = idOf(v.brand, model, year, engine);
        if (seen.has(id)) continue;
        seen.add(id);
        vehicles.push({ id, brand: v.brand, model, year, engine });
      }
      const kept = vehicles.slice(0, MAX_GARAGE);
      return { vehicles: kept, activeId: kept.some((x) => x.id === g.activeId) ? g.activeId : null };
    });
  },
  /** Al cerrar sesión: este navegador puede ser compartido. */
  clear() {
    store.set(EMPTY);
  },
};

/** "Chevrolet Aveo 2012 1.6", "Chevrolet Aveo" o "Chevrolet" (toda la marca). */
export const vehicleText = (v: MyVehicle | GarageVehicle) =>
  v ? [v.brand, v.model, v.year ?? null, v.model ? v.engine ?? null : null].filter(Boolean).join(" ") : "";

/** Enlace a los repuestos de ese vehículo. */
export const vehicleSearchHref = (v: { brand: string; model: string | null; year?: number | null; engine?: string | null }, extra: Record<string, string> = {}) => {
  const q = new URLSearchParams({
    marca: v.brand, ...(v.model ? { modelo: v.model } : {}), ...(v.model && v.year ? { anio: String(v.year) } : {}),
    ...(v.model && v.engine ? { motor: v.engine } : {}), ...extra,
  });
  return `/buscar?${q}`;
};
