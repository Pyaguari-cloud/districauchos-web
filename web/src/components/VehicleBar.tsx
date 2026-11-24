"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { sentenceCase } from "@/lib/format";
import { garage, useGarage, useMyVehicle, vehicleText, YEARS } from "@/lib/vehicle";
import { vehiclePhoto } from "@/lib/vehicle-photos";
import { PlateIcon } from "./icons";
import { PlateSearch } from "./PlateSearch";

/** engines: cilindrajes conocidos por modelo (solo los que tienen varios repuestos). */
export type VehicleOption = { brand: string; models: string[]; engines?: Record<string, string[]> };

const ALL = "__all__";

/**
 * "Mi garaje" bajo el encabezado, en todas las páginas: una sola barra con marca, modelo, año, cilindraje y
 * categoría (pedido del cliente 2026-11-21, como Mansuera). Al buscar, el vehículo queda guardado en el garaje
 * (máx. 5) y filtra las búsquedas siguientes. Año y cilindraje son opcionales y solo ocultan lo que sabemos que
 * es de otro año o motor. Listas nativas: en el celular abren el selector del sistema.
 * plates: muestra "Buscar por placa" (solo si el servidor tiene la clave del proveedor).
 */
export function VehicleBar({ options, categories, plates = false }: { options: VehicleOption[]; categories: string[]; plates?: boolean }) {
  const g = useGarage();
  const mine = useMyVehicle();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const onSearch = pathname === "/buscar";

  // La barra muestra el vehículo activo (y en resultados, la categoría filtrada ahora). Si cambian, se rearma.
  const urlCategory = onSearch ? params.get("categoria") ?? "" : "";
  const key = `${mine?.brand}|${mine?.model}|${mine?.year}|${mine?.engine}|${urlCategory}`;
  return <BarForm key={key} {...{ options, categories, g, mine, router, onSearch, params, urlCategory, plates }} />;
}

type FormProps = {
  options: VehicleOption[]; categories: string[]; g: ReturnType<typeof useGarage>; mine: ReturnType<typeof useMyVehicle>;
  router: ReturnType<typeof useRouter>; onSearch: boolean; params: ReturnType<typeof useSearchParams>; urlCategory: string;
  plates: boolean;
};

function BarForm({ options, categories, g, mine, router, onSearch, params, urlCategory, plates }: FormProps) {
  const [byPlate, setByPlate] = useState(false);
  const [brand, setBrand] = useState(mine?.brand ?? "");
  const [model, setModel] = useState(mine ? mine.model ?? ALL : "");
  const [year, setYear] = useState(mine?.year ? String(mine.year) : "");
  const [engine, setEngine] = useState(mine?.engine ?? "");
  const [category, setCategory] = useState(urlCategory);

  const option = options.find((o) => o.brand === brand);
  const realModel = model && model !== ALL ? model : "";
  const engines = (realModel && option?.engines?.[realModel]) || [];
  const photo = mine ? vehiclePhoto(mine.brand, mine.model) : null;
  const dirty = brand !== (mine?.brand ?? "") || realModel !== (mine?.model ?? "")
    || year !== (mine?.year ? String(mine.year) : "") || engine !== (mine?.engine ?? "") || category !== urlCategory;

  function search(e: React.FormEvent) {
    e.preventDefault();
    const y = realModel && year ? Number(year) : null;
    const eng = realModel && engine ? engine : null;
    if (brand) garage.addAndActivate(brand, realModel || null, y, eng);
    // En resultados se conserva lo que ya estaba (texto buscado, importación directa…); en otra página, búsqueda nueva.
    const q = new URLSearchParams(onSearch ? params : undefined);
    q.delete("pagina");
    const set = (k: string, v: string | number | null | undefined) => (v ? q.set(k, String(v)) : q.delete(k));
    set("marca", brand);
    set("modelo", realModel);
    set("anio", y);
    set("motor", eng);
    set("categoria", category);
    router.push(`/buscar?${q}`);
  }

  function clear() {
    garage.activate(null);
    setBrand(""); setModel(""); setYear(""); setEngine(""); setCategory("");
    if (!onSearch) return;
    const q = new URLSearchParams(params);
    for (const k of ["marca", "modelo", "anio", "motor", "categoria", "pagina"]) q.delete(k);
    router.push(`/buscar?${q}`);
  }

  const field = "flex min-w-0 flex-col gap-0.5 lg:flex-1";
  const label = "text-xs font-semibold text-white/70";
  const select =
    "h-11 w-full min-w-0 rounded-lg border border-white/20 bg-brand-ink px-2.5 text-base text-white focus:border-brand-yellow focus:outline-none disabled:opacity-50";
  const link = "text-sm text-white/80 underline underline-offset-2 hover:text-white";

  return (
    <div className="border-t border-white/10 bg-brand-ink">
      <form onSubmit={search} aria-labelledby="mi-garaje" className="mx-auto flex max-w-6xl flex-col gap-2 px-4 py-2.5">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
          <Link id="mi-garaje" href="/mi-garaje" className="font-display text-sm font-bold uppercase tracking-wide text-brand-yellow underline-offset-4 hover:underline">
            Mi garaje
          </Link>
          {g.vehicles.length > 1 && (
            <>
              <label htmlFor="vb-guardados" className="sr-only">Mis vehículos guardados</label>
              <select
                id="vb-guardados"
                className="h-8 max-w-[60vw] rounded-md border border-white/20 bg-brand-ink px-2 text-sm text-white"
                value={g.activeId ?? ""}
                onChange={(e) => {
                  const v = g.vehicles.find((x) => x.id === e.target.value);
                  if (!v) return clear();
                  garage.activate(v.id);
                  if (!onSearch) return;
                  const q = new URLSearchParams(params);
                  q.delete("pagina");
                  for (const [k, val] of [["marca", v.brand], ["modelo", v.model], ["anio", v.model && v.year], ["motor", v.model && v.engine]] as const)
                    if (val) q.set(k, String(val)); else q.delete(k);
                  router.push(`/buscar?${q}`);
                }}
              >
                {!g.activeId && <option value="">Mis vehículos ({g.vehicles.length})</option>}
                {g.vehicles.map((v) => <option key={v.id} value={v.id}>{vehicleText(v)}</option>)}
              </select>
            </>
          )}
          {plates && (
            // Más visible (cliente 2026-11-24: "no encontraban el botón de buscar placa porque estaba muy pequeño").
            <button type="button" onClick={() => setByPlate((v) => !v)} aria-expanded={byPlate}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-md bg-brand-yellow px-3 font-display text-sm font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark">
              <PlateIcon /> Buscar por placa
            </button>
          )}
          {mine && <button type="button" onClick={clear} className={link}>Quitar filtro</button>}
          <Link href="/mi-garaje" className={`${link} ml-auto`}>Administrar</Link>
        </div>

        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:flex lg:items-end">
          {photo && (
            <Image src={photo.src} alt="" width={72} height={45} sizes="72px"
              className="hidden h-11 w-[70px] shrink-0 rounded-md bg-white/10 object-cover lg:block" />
          )}
          <div className={field}>
            <label htmlFor="vb-marca" className={label}>Marca</label>
            <select id="vb-marca" className={select} value={brand}
              onChange={(e) => { setBrand(e.target.value); setModel(e.target.value ? ALL : ""); setEngine(""); }}>
              <option value="">Elige</option>
              {options.map((o) => <option key={o.brand} value={o.brand}>{o.brand}</option>)}
            </select>
          </div>
          <div className={field}>
            <label htmlFor="vb-modelo" className={label}>Modelo</label>
            <select id="vb-modelo" className={select} value={model} disabled={!brand}
              onChange={(e) => { setModel(e.target.value); setEngine(""); }}>
              {!brand && <option value="">Primero la marca</option>}
              {brand && <option value={ALL}>Todos los {brand}</option>}
              {option?.models.map((m) => <option key={m} value={m}>{m}</option>)}
            </select>
          </div>
          <div className={field}>
            <label htmlFor="vb-anio" className={label}>Año</label>
            <select id="vb-anio" className={select} value={year} disabled={!realModel} onChange={(e) => setYear(e.target.value)}>
              <option value="">{realModel ? "Todos" : "Elige el modelo"}</option>
              {YEARS.map((y) => <option key={y} value={y}>{y}</option>)}
            </select>
          </div>
          <div className={field}>
            <label htmlFor="vb-motor" className={label}>Cilindraje</label>
            <select id="vb-motor" className={select} value={engine} disabled={engines.length === 0} onChange={(e) => setEngine(e.target.value)}>
              <option value="">{!realModel ? "Elige el modelo" : engines.length ? "Todos" : "Sin datos"}</option>
              {engines.map((x) => <option key={x} value={x}>{x}</option>)}
            </select>
          </div>
          <div className={field}>
            <label htmlFor="vb-categoria" className={label}>Categoría</label>
            <select id="vb-categoria" className={select} value={category} onChange={(e) => setCategory(e.target.value)}>
              <option value="">Todas</option>
              {categories.map((c) => <option key={c} value={c}>{sentenceCase(c)}</option>)}
            </select>
          </div>
          <button type="submit" disabled={!brand && !category}
            className={`h-11 rounded-lg px-5 font-display font-bold uppercase tracking-wide lg:shrink-0 ${
              dirty || !onSearch ? "bg-brand-yellow text-brand-black hover:bg-brand-yellow-dark" : "bg-white/15 text-white"
            } disabled:opacity-40`}>
            Buscar
          </button>
        </div>
      </form>
      {plates && byPlate && (
        <div className="mx-auto max-w-6xl px-4 pb-3">
          <PlateSearch options={options} onClose={() => setByPlate(false)} />
        </div>
      )}
    </div>
  );
}
