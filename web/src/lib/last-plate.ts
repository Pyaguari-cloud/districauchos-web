"use client";

import { createLocalStore } from "./local-store";

/**
 * Última placa consultada en este navegador (solo datos del vehículo: placa y chasis). Al pagar, si el vehículo del
 * garaje es el mismo, la placa y el chasis van solos en el pedido para que el vendedor confirme el OEM.
 */
export type LastPlate = {
  plate: string; vin: string | null; brand: string; model: string | null;
  // Tal como lo da la ANT, para la pregunta a la IA de Google desde los resultados de búsqueda.
  make?: string | null; modelName?: string | null; year?: number | null; engine?: string | null;
};
const str = (v: unknown, max: number) => (typeof v === "string" && v ? v.slice(0, max) : null);

const store = createLocalStore<LastPlate | null>("dc.lastPlate.v1", null, (raw) => {
  const v = raw as Partial<LastPlate> | null;
  if (!v || typeof v.plate !== "string" || typeof v.brand !== "string") return null;
  return {
    plate: v.plate.slice(0, 10), brand: v.brand, model: typeof v.model === "string" ? v.model : null,
    vin: typeof v.vin === "string" && /^[A-Z0-9]{17}$/.test(v.vin) ? v.vin : null,
    make: str(v.make, 40), modelName: str(v.modelName, 80), engine: str(v.engine, 10),
    year: typeof v.year === "number" && v.year > 1950 && v.year < 2100 ? v.year : null,
  };
});

export const saveLastPlate = (p: LastPlate) => store.set(p);
export const clearLastPlate = () => store.set(null);
export const useLastPlate = store.use;
