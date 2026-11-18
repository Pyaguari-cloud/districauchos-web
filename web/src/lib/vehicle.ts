"use client";

import { createLocalStore } from "./local-store";

/** Vehículo del cliente, recordado entre visitas (brief §9). */
export type MyVehicle = { brand: string; model: string | null } | null;

const store = createLocalStore<MyVehicle>("dc.vehicle.v1", null, (v) => {
  const x = v as { brand?: unknown; model?: unknown } | null;
  return x && typeof x.brand === "string" && x.brand
    ? { brand: x.brand, model: typeof x.model === "string" && x.model ? x.model : null }
    : null;
});

export const useMyVehicle = store.use;
export const setMyVehicle = (v: MyVehicle) => store.set(v);
export const vehicleText = (v: MyVehicle) => (v ? (v.model ? `${v.brand} ${v.model}` : v.brand) : "");
