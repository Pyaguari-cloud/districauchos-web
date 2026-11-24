"use client";

import { createLocalStore } from "./local-store";

/**
 * Última placa consultada en este navegador (solo datos del vehículo: placa y chasis). Al pagar, si el vehículo del
 * garaje es el mismo, la placa y el chasis van solos en el pedido para que el vendedor confirme el OEM.
 */
export type LastPlate = { plate: string; vin: string | null; brand: string; model: string | null };

const store = createLocalStore<LastPlate | null>("dc.lastPlate.v1", null, (raw) => {
  const v = raw as Partial<LastPlate> | null;
  if (!v || typeof v.plate !== "string" || typeof v.brand !== "string") return null;
  return {
    plate: v.plate.slice(0, 10), brand: v.brand, model: typeof v.model === "string" ? v.model : null,
    vin: typeof v.vin === "string" && /^[A-Z0-9]{17}$/.test(v.vin) ? v.vin : null,
  };
});

export const saveLastPlate = (p: LastPlate) => store.set(p);
export const clearLastPlate = () => store.set(null);
export const useLastPlate = store.use;
