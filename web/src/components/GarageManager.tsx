"use client";

import Image from "next/image";
import Link from "next/link";
import { useEffect, useState } from "react";
import { sentenceCase } from "@/lib/format";
import {
  garage, MAX_GARAGE, useGarage, vehicleSearchHref, vehicleText, YEARS, type GarageVehicle,
} from "@/lib/vehicle";
import { photoCredit, vehiclePhoto } from "@/lib/vehicle-photos";
import type { VehicleOption } from "./VehicleBar";

type Parts = { total: number; categories: { name: string; count: number }[] };

const select = "min-h-12 w-full rounded-lg border border-line bg-white px-3 text-base";
const button = "inline-flex min-h-11 items-center justify-center rounded-lg px-4 font-semibold";

/** Página "Mi garaje": vehículo activo en grande, los demás guardados, agregar y repuestos por categoría. */
export function GarageManager({ options }: { options: VehicleOption[] }) {
  const g = useGarage();
  const active = g.vehicles.find((v) => v.id === g.activeId) ?? null;
  const [adding, setAdding] = useState(false);
  const showForm = adding || g.vehicles.length === 0;

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="font-display text-3xl font-bold uppercase tracking-wide">Mi garaje</h1>
        <p className="mt-1 max-w-2xl text-muted">
          Guarda hasta {MAX_GARAGE} vehículos. El que elijas filtra las búsquedas y va en tu cotización y en tu pedido.
          Se guardan en este navegador, sin crear cuenta.
        </p>
      </div>

      {active ? (
        <ActiveVehicle v={active} />
      ) : g.vehicles.length > 0 ? (
        <p className="rounded-lg bg-surface px-4 py-3">Elige uno de tus vehículos para ver sus repuestos.</p>
      ) : null}

      {g.vehicles.length > 0 && (
        <section aria-labelledby="mis-vehiculos" className="flex flex-col gap-3">
          <h2 id="mis-vehiculos" className="font-display text-xl font-bold uppercase tracking-wide">
            Tus vehículos ({g.vehicles.length}/{MAX_GARAGE})
          </h2>
          <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
            {g.vehicles.map((v) => (
              <li key={v.id}>
                <VehicleCard v={v} active={v.id === g.activeId} />
              </li>
            ))}
            {!showForm && (
              <li>
                <button
                  type="button" onClick={() => setAdding(true)}
                  className="flex h-full min-h-32 w-full flex-col items-center justify-center gap-1 rounded-xl border-2 border-dashed border-line bg-white p-3 font-semibold hover:border-gray-500"
                >
                  <span aria-hidden="true" className="text-3xl leading-none">＋</span>
                  Agregar vehículo
                  {g.vehicles.length >= MAX_GARAGE && <span className="text-xs font-normal text-muted">(reemplaza el más antiguo)</span>}
                </button>
              </li>
            )}
          </ul>
        </section>
      )}

      {showForm && <AddVehicle options={options} onDone={() => setAdding(false)} canCancel={g.vehicles.length > 0} />}

      {active && <PartsFor v={active} />}
    </div>
  );
}

function ActiveVehicle({ v }: { v: GarageVehicle }) {
  const photo = vehiclePhoto(v.brand, v.model);
  return (
    <section aria-label="Vehículo activo" className="grid gap-4 overflow-hidden rounded-xl border border-line bg-white p-4 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)] sm:items-center">
      <figure className="flex flex-col gap-1">
        {photo ? (
          <Image src={photo.src} alt={vehicleText(v)} width={640} height={400} sizes="(min-width: 640px) 50vw, 100vw" priority
            className="aspect-[8/5] w-full rounded-lg object-cover" />
        ) : (
          <div className="flex aspect-[8/5] w-full items-center justify-center rounded-lg bg-surface text-6xl" aria-hidden="true">🚗</div>
        )}
        {photo && (
          <figcaption>
            <Link href="/creditos" className="text-xs text-muted underline-offset-2 hover:underline">{photoCredit(photo)}</Link>
          </figcaption>
        )}
      </figure>
      <div className="flex min-w-0 flex-col gap-3">
        <div>
          <p className="text-sm font-semibold uppercase tracking-wide text-ok">✓ Vehículo activo</p>
          <p className="font-display text-4xl font-bold uppercase leading-tight tracking-wide [overflow-wrap:anywhere]">
            {v.model ?? `Todos los ${v.brand}`}
          </p>
          <p className="text-xl text-muted">{v.model ? v.brand : "Toda la marca"}{v.year ? ` ${v.year}` : ""}</p>
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="g-year" className="text-sm font-semibold">Año (opcional)</label>
          <select id="g-year" className={select} value={v.year ?? ""} onChange={(e) => garage.setYear(v.id, e.target.value ? Number(e.target.value) : null)}>
            <option value="">Sin año</option>
            {YEARS.map((y) => <option key={y} value={y}>{y}</option>)}
          </select>
          <p className="text-xs text-muted">Lo enviamos con tu cotización para que el vendedor confirme que el repuesto es el correcto.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Link href={vehicleSearchHref(v)} className={`${button} bg-brand-yellow font-display uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark`}>
            Ver sus repuestos
          </Link>
          <button type="button" onClick={() => garage.activate(null)} className={`${button} border border-line bg-white hover:border-gray-500`}>
            Buscar sin filtro
          </button>
          <button type="button" onClick={() => garage.remove(v.id)} className={`${button} text-brand-red underline underline-offset-2`}
            aria-label={`Quitar ${vehicleText(v)} de mi garaje`}>
            Quitar
          </button>
        </div>
      </div>
    </section>
  );
}

function VehicleCard({ v, active }: { v: GarageVehicle; active: boolean }) {
  const photo = vehiclePhoto(v.brand, v.model);
  return (
    <button
      type="button" onClick={() => garage.activate(v.id)} aria-pressed={active}
      className={`flex h-full w-full flex-col overflow-hidden rounded-xl border-2 bg-white text-left ${active ? "border-brand-black" : "border-line hover:border-gray-500"}`}
    >
      {photo ? (
        <Image src={photo.src} alt="" width={320} height={200} sizes="(min-width: 1024px) 20vw, 50vw" className="aspect-[8/5] w-full object-cover" />
      ) : (
        <span className="flex aspect-[8/5] w-full items-center justify-center bg-surface text-4xl" aria-hidden="true">🚗</span>
      )}
      <span className="flex flex-col p-2">
        <span className="font-semibold leading-tight [overflow-wrap:anywhere]">{vehicleText(v)}</span>
        <span className={`text-xs ${active ? "font-semibold text-ok" : "text-muted"}`}>{active ? "✓ Activo" : "Toca para elegirlo"}</span>
      </span>
    </button>
  );
}

function AddVehicle({ options, onDone, canCancel }: { options: VehicleOption[]; onDone: () => void; canCancel: boolean }) {
  const [brand, setBrand] = useState("");
  const [model, setModel] = useState("");
  const [year, setYear] = useState("");
  const [engine, setEngine] = useState("");
  const option = options.find((o) => o.brand === brand);
  const models = option?.models ?? [];
  const engines = (model && option?.engines?.[model]) || [];
  const ALL = "__all__";

  function save(e: React.FormEvent) {
    e.preventDefault();
    if (!brand || !model) return;
    garage.addAndActivate(brand, model === ALL ? null : model, year ? Number(year) : null, engine || null);
    setBrand(""); setModel(""); setYear(""); setEngine("");
    onDone();
  }

  return (
    <form onSubmit={save} id="agregar" className="flex flex-col gap-4 rounded-xl border border-line bg-white p-4">
      <h2 className="font-display text-xl font-bold uppercase tracking-wide">Agregar un vehículo</h2>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="flex flex-col gap-1">
          <label htmlFor="a-brand" className="font-semibold">Marca</label>
          <select id="a-brand" className={select} value={brand} onChange={(e) => { setBrand(e.target.value); setModel(""); setEngine(""); }} required>
            <option value="">Elige la marca</option>
            {options.map((o) => <option key={o.brand} value={o.brand}>{o.brand}</option>)}
          </select>
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="a-model" className="font-semibold">Modelo</label>
          <select id="a-model" className={select} value={model} onChange={(e) => { setModel(e.target.value); setEngine(""); }} disabled={!brand} required>
            <option value="">{brand ? "Elige el modelo" : "Primero la marca"}</option>
            {brand && <option value={ALL}>Todos los {brand}</option>}
            {models.map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="a-year" className="font-semibold">Año <span className="font-normal text-muted">(opcional)</span></label>
          <select id="a-year" className={select} value={year} onChange={(e) => setYear(e.target.value)}>
            <option value="">Sin año</option>
            {YEARS.map((y) => <option key={y} value={y}>{y}</option>)}
          </select>
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="a-engine" className="font-semibold">Cilindraje <span className="font-normal text-muted">(opcional)</span></label>
          <select id="a-engine" className={select} value={engine} onChange={(e) => setEngine(e.target.value)} disabled={engines.length === 0}>
            <option value="">{engines.length ? "Sin cilindraje" : "Sin datos para este modelo"}</option>
            {engines.map((x) => <option key={x} value={x}>{x}</option>)}
          </select>
        </div>
      </div>
      <p className="text-sm text-muted">
        ¿No está tu modelo? Elige &quot;Todos los …&quot; de tu marca, o escríbenos por WhatsApp.
      </p>
      <div className="flex flex-wrap gap-2">
        <button type="submit" disabled={!brand || !model}
          className={`${button} bg-brand-black font-display uppercase tracking-wide text-white disabled:opacity-40`}>
          Guardar vehículo
        </button>
        {canCancel && <button type="button" onClick={onDone} className={`${button} underline underline-offset-2`}>Cancelar</button>}
      </div>
    </form>
  );
}

/** "Partes y accesorios para tu vehículo" de Amazon: categorías con la cantidad de repuestos de ese carro. */
function PartsFor({ v }: { v: GarageVehicle }) {
  const key = `${v.brand}|${v.model ?? ""}`;
  // La respuesta se guarda con el vehículo al que pertenece: al cambiar de vehículo se ve "Cargando…" sola.
  const [result, setResult] = useState<{ key: string; parts: Parts | null } | null>(null);
  const current = result?.key === key ? result : null;
  const parts = current?.parts ?? null;
  const failed = current !== null && current.parts === null;

  useEffect(() => {
    let cancelled = false;
    const q = new URLSearchParams({ marca: v.brand, ...(v.model ? { modelo: v.model } : {}) });
    fetch(`/api/garaje?${q}`)
      .then((r) => (r.ok ? r.json() : Promise.reject()))
      .then((d: Parts) => { if (!cancelled) setResult({ key, parts: d }); })
      .catch(() => { if (!cancelled) setResult({ key, parts: null }); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- key resume marca y modelo
  }, [key]);

  const name = v.model ? `${v.brand} ${v.model}` : v.brand;
  return (
    <section aria-labelledby="sus-repuestos" className="flex flex-col gap-3">
      <h2 id="sus-repuestos" className="font-display text-xl font-bold uppercase tracking-wide">
        Repuestos para tu {name}{parts ? ` (${parts.total.toLocaleString("es-EC")})` : ""}
      </h2>
      {failed ? (
        <Link href={vehicleSearchHref(v)} className="font-semibold underline underline-offset-4">Ver los repuestos de tu {name}</Link>
      ) : !parts ? (
        <p className="text-muted">Cargando…</p>
      ) : parts.total === 0 ? (
        <p className="text-muted">Todavía no tenemos repuestos marcados para este vehículo. Pregúntanos por WhatsApp.</p>
      ) : (
        <ul className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4">
          {parts.categories.map((c) => (
            <li key={c.name}>
              <Link href={vehicleSearchHref(v, { categoria: c.name })}
                className="flex min-h-12 items-center justify-between gap-2 rounded-lg border border-line bg-white px-3 py-2 hover:border-gray-500">
                <span className="min-w-0 [overflow-wrap:anywhere]">{sentenceCase(c.name)}</span>
                <span className="shrink-0 text-sm text-muted">{c.count}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
