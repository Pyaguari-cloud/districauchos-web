"use client";

import { createLocalStore } from "./local-store";

/**
 * "Mi garaje" (como el de Amazon): hasta 5 vehículos guardados y uno activo.
 * El activo filtra las búsquedas, va en la cotización y marca la compatibilidad en la ficha.
 */
export type GarageVehicle = { id: string; brand: string; model: string | null };
export type MyVehicle = { brand: string; model: string | null } | null;
type Garage = { vehicles: GarageVehicle[]; activeId: string | null };

export const MAX_GARAGE = 5;
const EMPTY: Garage = { vehicles: [], activeId: null };

const idOf = (brand: string, model: string | null) => `${brand}|${model ?? ""}`;

const store = createLocalStore<Garage>("dc.garage.v1", EMPTY, (v) => {
  const g = v as Partial<Garage> | null;
  const vehicles = (Array.isArray(g?.vehicles) ? g!.vehicles : [])
    .filter((x): x is GarageVehicle => !!x && typeof x.brand === "string" && x.brand.length > 0)
    .map((x) => ({ id: idOf(x.brand, typeof x.model === "string" && x.model ? x.model : null), brand: x.brand, model: x.model || null }))
    .slice(0, MAX_GARAGE);
  const activeId = vehicles.some((x) => x.id === g?.activeId) ? g!.activeId! : null;
  return { vehicles, activeId };
});

export const useGarage = store.use;

/** Vehículo activo (o null = sin filtro). */
export function useMyVehicle(): MyVehicle {
  const g = store.use();
  const v = g.vehicles.find((x) => x.id === g.activeId);
  return v ? { brand: v.brand, model: v.model } : null;
}

export const garage = {
  /** Agrega (o reutiliza si ya existe) y lo deja activo. Si está lleno, reemplaza el más antiguo. */
  addAndActivate(brand: string, model: string | null) {
    const id = idOf(brand, model);
    store.set((g) => {
      const rest = g.vehicles.filter((x) => x.id !== id);
      const vehicles = [{ id, brand, model }, ...rest].slice(0, MAX_GARAGE);
      return { vehicles, activeId: id };
    });
  },
  activate(id: string | null) {
    store.set((g) => ({ ...g, activeId: id && g.vehicles.some((x) => x.id === id) ? id : null }));
  },
  remove(id: string) {
    store.set((g) => ({ vehicles: g.vehicles.filter((x) => x.id !== id), activeId: g.activeId === id ? null : g.activeId }));
  },
};

export const vehicleText = (v: MyVehicle | GarageVehicle) => (v ? (v.model ? `${v.brand} ${v.model}` : v.brand) : "");
