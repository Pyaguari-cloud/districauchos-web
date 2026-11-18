"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { garage, MAX_GARAGE, useGarage, vehicleText, type MyVehicle } from "@/lib/vehicle";

export type VehicleOption = { brand: string; models: string[] };

const NONE = "__none__";
const ADD = "__add__";

/**
 * "Mi garaje" bajo el encabezado, en todas las páginas: elegir el vehículo activo,
 * agregar otro (máx. 5) o quitarlo. Listas nativas: en el celular abren el selector del sistema.
 */
export function VehicleBar({ options }: { options: VehicleOption[] }) {
  const g = useGarage();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [adding, setAdding] = useState(false);
  const [brand, setBrand] = useState("");

  const active = g.vehicles.find((v) => v.id === g.activeId) ?? null;
  const showForm = adding || g.vehicles.length === 0;
  const models = options.find((o) => o.brand === brand)?.models ?? [];

  /** En resultados el cambio se aplica de inmediato; en otras páginas solo se recuerda. */
  function applyToSearch(v: MyVehicle) {
    if (pathname !== "/buscar") return;
    const q = new URLSearchParams(params);
    q.delete("pagina");
    if (v) q.set("marca", v.brand); else q.delete("marca");
    if (v?.model) q.set("modelo", v.model); else q.delete("modelo");
    router.push(`/buscar?${q}`);
  }

  function save(model: string | null) {
    garage.addAndActivate(brand, model);
    applyToSearch({ brand, model });
    setAdding(false);
    setBrand("");
  }

  const select =
    "h-11 min-w-0 flex-1 rounded-lg border border-white/20 bg-brand-ink px-3 text-base text-white focus:border-brand-yellow focus:outline-none sm:flex-none sm:w-52";
  const link = "h-11 rounded-lg px-2 text-sm text-white/80 underline underline-offset-2 hover:text-white";

  return (
    <div className="border-t border-white/10 bg-brand-ink">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-2 px-4 py-2">
        <span id="mi-garaje" className="w-full font-display text-sm font-bold uppercase tracking-wide text-brand-yellow sm:w-auto">
          Mi garaje
        </span>

        {showForm ? (
          <>
            <label htmlFor="vb-marca" className="sr-only">Marca de tu vehículo</label>
            <select id="vb-marca" className={select} value={brand} onChange={(e) => setBrand(e.target.value)} aria-describedby="mi-garaje">
              <option value="">Marca</option>
              {options.map((o) => <option key={o.brand} value={o.brand}>{o.brand}</option>)}
            </select>
            <label htmlFor="vb-modelo" className="sr-only">Modelo de tu vehículo</label>
            <select
              id="vb-modelo"
              className={select}
              value=""
              disabled={!brand}
              onChange={(e) => save(e.target.value === NONE ? null : e.target.value)}
              aria-describedby="mi-garaje"
            >
              <option value="">{brand ? "Elige el modelo" : "Modelo"}</option>
              {brand && <option value={NONE}>Todos los {brand}</option>}
              {models.map((m) => <option key={m} value={m}>{m}</option>)}
            </select>
            {g.vehicles.length > 0 && (
              <button type="button" className={link} onClick={() => { setAdding(false); setBrand(""); }}>Cancelar</button>
            )}
          </>
        ) : (
          <>
            <label htmlFor="vb-activo" className="sr-only">Vehículo activo</label>
            <select
              id="vb-activo"
              className={select}
              value={g.activeId ?? NONE}
              aria-describedby="mi-garaje"
              onChange={(e) => {
                if (e.target.value === ADD) return setAdding(true);
                const id = e.target.value === NONE ? null : e.target.value;
                garage.activate(id);
                const v = g.vehicles.find((x) => x.id === id);
                applyToSearch(v ? { brand: v.brand, model: v.model } : null);
              }}
            >
              {g.vehicles.map((v) => <option key={v.id} value={v.id}>{vehicleText(v)}</option>)}
              <option value={NONE}>Sin filtro (todos los vehículos)</option>
              <option value={ADD}>{g.vehicles.length >= MAX_GARAGE ? "＋ Agregar (reemplaza el más antiguo)" : "＋ Agregar otro vehículo"}</option>
            </select>
            {active && (
              <button
                type="button"
                className={link}
                onClick={() => { garage.remove(active.id); applyToSearch(null); }}
                aria-label={`Quitar ${vehicleText(active)} de mi garaje`}
              >
                Quitar
              </button>
            )}
          </>
        )}
      </div>
    </div>
  );
}
