"use client";

import { useId, useRef, useState } from "react";
import { PlateIcon } from "./icons";
import { PlateSearch } from "./PlateSearch";
import { SearchBox } from "./SearchBox";
import type { VehicleOption } from "./VehicleBar";

type Tab = "placa" | "nombre";

/**
 * "Encuentra tu repuesto" del inicio, junto a las promociones (pedido del cliente 2026-11-24, como Mansuera: la
 * búsqueda por placa en grande, porque el botón chico no lo encontraban). Pestañas con el patrón ARIA de "tabs":
 * flechas ← → para cambiar, y la pestaña activa es la única con tabIndex 0.
 * id="buscar": a esto llevan "Buscar por placa" del menú y de la promoción.
 */
export function FinderCard({ options, plates }: { options: VehicleOption[]; plates: boolean }) {
  const [tab, setTab] = useState<Tab>(plates ? "placa" : "nombre");
  const base = useId();
  const tabs: { id: Tab; label: string }[] = plates
    ? [{ id: "placa", label: "Por placa" }, { id: "nombre", label: "Por nombre o código" }]
    : [{ id: "nombre", label: "Por nombre o código" }];
  const refs = useRef<Record<Tab, HTMLButtonElement | null>>({ placa: null, nombre: null });

  function onKey(e: React.KeyboardEvent) {
    if (e.key !== "ArrowRight" && e.key !== "ArrowLeft") return;
    const i = tabs.findIndex((t) => t.id === tab);
    const next = tabs[(i + (e.key === "ArrowRight" ? 1 : -1) + tabs.length) % tabs.length].id;
    setTab(next);
    refs.current[next]?.focus();
  }

  const benefits = [
    { t: "Repuesto exacto", d: "Para tu marca, modelo y año" },
    { t: "Al instante", d: "Sin esperar a un vendedor" },
    { t: "Compra segura", d: "Tarjeta o WhatsApp" },
  ];

  return (
    <section id="buscar" aria-labelledby={`${base}-t`} className="scroll-mt-4 flex flex-col gap-4 rounded-2xl bg-brand-ink p-5 text-white sm:p-7">
      <div>
        <h1 id={`${base}-t`} className="font-display text-3xl font-bold uppercase leading-tight tracking-wide sm:text-4xl">
          Encuentra el repuesto <span className="text-brand-yellow">para tu vehículo</span>
        </h1>
        <p className="mt-1 text-gray-300">
          {plates ? "Escribe tu placa y te mostramos los repuestos que le sirven a tu carro." : "Busca por nombre, por vehículo o por código OEM."}
        </p>
      </div>

      {tabs.length > 1 && (
        <div role="tablist" aria-label="Cómo quieres buscar" onKeyDown={onKey} className="grid grid-cols-2 gap-1 rounded-xl bg-black/40 p-1">
          {tabs.map((t) => (
            <button key={t.id} ref={(el) => { refs.current[t.id] = el; }} type="button" role="tab" id={`${base}-${t.id}`}
              aria-selected={tab === t.id} aria-controls={`${base}-${t.id}-p`} tabIndex={tab === t.id ? 0 : -1}
              onClick={() => setTab(t.id)}
              className={`flex min-h-11 items-center justify-center gap-2 rounded-lg px-3 font-display font-bold uppercase tracking-wide transition ${
                tab === t.id ? "bg-brand-yellow text-brand-black" : "text-white/80 hover:text-white"}`}>
              {t.id === "placa" && <PlateIcon />}{t.label}
            </button>
          ))}
        </div>
      )}

      {plates && (
        <div role="tabpanel" id={`${base}-placa-p`} aria-labelledby={`${base}-placa`} hidden={tab !== "placa"}>
          <PlateSearch options={options} variant="hero" />
        </div>
      )}
      <div role="tabpanel" id={`${base}-nombre-p`} aria-labelledby={`${base}-nombre`} hidden={tab !== "nombre"} className="flex flex-col gap-2">
        <SearchBox size="lg" />
        <p className="text-sm text-gray-400">Ej.: “manguera radiador Aveo” o el código OEM “96807000”.</p>
      </div>

      <ul className="mt-auto grid grid-cols-3 gap-2 border-t border-white/10 pt-4 text-center">
        {benefits.map((b) => (
          <li key={b.t}>
            <p className="font-display text-sm font-bold uppercase tracking-wide text-brand-yellow sm:text-base">{b.t}</p>
            <p className="text-xs leading-snug text-gray-400">{b.d}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
