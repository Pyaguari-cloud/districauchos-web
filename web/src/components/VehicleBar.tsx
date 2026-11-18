"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { setMyVehicle, useMyVehicle } from "@/lib/vehicle";

export type VehicleOption = { brand: string; models: string[] };

/**
 * "Mi vehículo": marca y modelo recordados en todas las páginas (brief §9).
 * Listas nativas: en el celular abren el selector del sistema, que es el más cómodo.
 */
export function VehicleBar({ options }: { options: VehicleOption[] }) {
  const vehicle = useMyVehicle();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();

  const models = options.find((o) => o.brand === vehicle?.brand)?.models ?? [];

  function choose(brand: string | null, model: string | null) {
    const next = brand ? { brand, model } : null;
    setMyVehicle(next);
    // En resultados se aplica de inmediato; en otras páginas solo se recuerda.
    if (pathname === "/buscar") {
      const q = new URLSearchParams(params);
      q.delete("pagina");
      if (next) q.set("marca", next.brand); else q.delete("marca");
      if (next?.model) q.set("modelo", next.model); else q.delete("modelo");
      router.push(`/buscar?${q}`);
    }
  }

  const selectClass =
    "h-11 min-w-0 flex-1 rounded-lg border border-white/20 bg-brand-ink px-3 text-base text-white focus:border-brand-yellow focus:outline-none sm:flex-none sm:w-48";

  return (
    <div className="border-t border-white/10 bg-brand-ink">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-2 px-4 py-2">
        <span id="mi-vehiculo" className="w-full font-display text-sm font-bold uppercase tracking-wide text-brand-yellow sm:w-auto">
          Mi vehículo
        </span>
        <label htmlFor="vb-marca" className="sr-only">Marca</label>
        <select
          id="vb-marca"
          aria-describedby="mi-vehiculo"
          className={selectClass}
          value={vehicle?.brand ?? ""}
          onChange={(e) => choose(e.target.value || null, null)}
        >
          <option value="">Marca</option>
          {options.map((o) => <option key={o.brand} value={o.brand}>{o.brand}</option>)}
        </select>
        <label htmlFor="vb-modelo" className="sr-only">Modelo</label>
        <select
          id="vb-modelo"
          aria-describedby="mi-vehiculo"
          className={selectClass}
          value={vehicle?.model ?? ""}
          disabled={!vehicle || models.length === 0}
          onChange={(e) => vehicle && choose(vehicle.brand, e.target.value || null)}
        >
          <option value="">{vehicle ? "Todos los modelos" : "Modelo"}</option>
          {models.map((m) => <option key={m} value={m}>{m}</option>)}
        </select>
        {vehicle && (
          <button
            type="button"
            onClick={() => choose(null, null)}
            className="h-11 rounded-lg px-3 text-sm text-white/80 underline underline-offset-2 hover:text-white"
          >
            Quitar
          </button>
        )}
      </div>
    </div>
  );
}
