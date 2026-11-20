"use client";

import { createLocalStore } from "./local-store";

/**
 * "Mi garaje" (como el de Amazon): hasta 5 vehículos guardados y uno activo.
 * El activo filtra las búsquedas, va en la cotización y marca la compatibilidad en la ficha.
 * El año es opcional: hoy no filtra (el inventario no tiene años) pero viaja en la cotización y en el pedido
 * para que el vendedor confirme; servirá para filtrar cuando existan las compatibilidades por OEM.
 */
export type GarageVehicle = { id: string; brand: string; model: string | null; year: number | null };
export type MyVehicle = { brand: string; model: string | null; year?: number | null } | null;
type Garage = { vehicles: GarageVehicle[]; activeId: string | null };

export const MAX_GARAGE = 5;
export const MIN_YEAR = 1970;
export const MAX_YEAR = new Date().getFullYear() + 1;
/** Años para elegir, del más nuevo al más viejo. */
export const YEARS = Array.from({ length: MAX_YEAR - MIN_YEAR + 1 }, (_, i) => MAX_YEAR - i);

const EMPTY: Garage = { vehicles: [], activeId: null };

const cleanYear = (y: unknown) => (typeof y === "number" && Number.isInteger(y) && y >= MIN_YEAR && y <= MAX_YEAR ? y : null);
const idOf = (brand: string, model: string | null, year: number | null) => `${brand}|${model ?? ""}|${year ?? ""}`;

// Misma clave que antes: los garajes ya guardados (sin año) siguen funcionando.
const store = createLocalStore<Garage>("dc.garage.v1", EMPTY, (v) => {
  const g = v as Partial<Garage> | null;
  const raw = Array.isArray(g?.vehicles) ? g!.vehicles : [];
  const vehicles = raw
    .filter((x): x is GarageVehicle => !!x && typeof x.brand === "string" && x.brand.length > 0)
    .map((x) => {
      const model = typeof x.model === "string" && x.model ? x.model : null;
      const year = cleanYear(x.year);
      return { id: idOf(x.brand, model, year), brand: x.brand, model, year };
    })
    .slice(0, MAX_GARAGE);
  // Garajes viejos guardaban un id sin el año: se ubica el vehículo guardado y se recalcula su id.
  const a = raw.find((x) => !!x && (x as GarageVehicle).id === g?.activeId) as GarageVehicle | undefined;
  const aId = a && typeof a.brand === "string" ? idOf(a.brand, a.model || null, cleanYear(a.year)) : null;
  const activeId = aId && vehicles.some((x) => x.id === aId) ? aId : null;
  return { vehicles, activeId };
});

export const useGarage = store.use;

/** Vehículo activo (o null = sin filtro). */
export function useMyVehicle(): MyVehicle {
  const g = store.use();
  const v = g.vehicles.find((x) => x.id === g.activeId);
  return v ? { brand: v.brand, model: v.model, year: v.year } : null;
}

export const garage = {
  /** Agrega (o reutiliza si ya existe) y lo deja activo. Si está lleno, reemplaza el más antiguo. */
  addAndActivate(brand: string, model: string | null, year: number | null = null) {
    const y = cleanYear(year);
    const id = idOf(brand, model, y);
    store.set((g) => {
      const rest = g.vehicles.filter((x) => x.id !== id);
      const vehicles = [{ id, brand, model, year: y }, ...rest].slice(0, MAX_GARAGE);
      return { vehicles, activeId: id };
    });
  },
  /** Cambia el año de un vehículo guardado (sigue activo si lo estaba). */
  setYear(id: string, year: number | null) {
    store.set((g) => {
      const v = g.vehicles.find((x) => x.id === id);
      if (!v) return g;
      const y = cleanYear(year);
      const newId = idOf(v.brand, v.model, y);
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
};

/** "Chevrolet Aveo 2012", "Chevrolet Aveo" o "Chevrolet" (toda la marca). */
export const vehicleText = (v: MyVehicle | GarageVehicle) =>
  v ? [v.brand, v.model, v.year ?? null].filter(Boolean).join(" ") : "";

/** Enlace a los repuestos de ese vehículo. */
export const vehicleSearchHref = (v: { brand: string; model: string | null }, extra: Record<string, string> = {}) => {
  const q = new URLSearchParams({ marca: v.brand, ...(v.model ? { modelo: v.model } : {}), ...extra });
  return `/buscar?${q}`;
};
