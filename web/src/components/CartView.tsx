"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import type { QuoteResponse } from "@/lib/quote";
import { cart, useCart, type CartItem } from "@/lib/cart";
import { createLocalStore } from "@/lib/local-store";
import type { Delivery } from "@/lib/quote";
import { negocio } from "@/content/negocio";
import { money } from "@/lib/format";
import { useMyVehicle, vehicleText } from "@/lib/vehicle";
import { QtyStepper } from "./AddToCart";
import { WhatsAppButton } from "./WhatsAppButton";

const deliveryStore = createLocalStore<Delivery>("dc.delivery.v1", "none", (v) =>
  v === "pickup" || v === "quito" || v === "national" ? v : "none",
);

const DELIVERY_OPTIONS: { value: Exclude<Delivery, "none">; label: string; note: string }[] = [
  { value: "pickup", label: "Retiro en local", note: "Sin costo" },
  { value: "quito", label: "Envío dentro de Quito", note: "Gratis" },
  { value: "national", label: "Envío a provincia", note: `Por cooperativa, +$${negocio.shipping.national.fee}` },
];

type State = { status: "idle" | "loading" | "ok" | "error"; quote?: QuoteResponse; forPayload?: string };

/** Carrito: los precios mostrados son siempre los que calcula el servidor. */
export function CartView() {
  const items = useCart();
  const vehicle = useMyVehicle();
  const delivery = deliveryStore.use();
  const [state, setState] = useState<State>({ status: "idle" });

  const payload = JSON.stringify({
    items: items.map(({ oem, code, qty }) => ({ oem, code, qty })),
    vehicle: vehicleText(vehicle) || null,
    delivery: delivery === "none" ? null : delivery,
  });

  useEffect(() => {
    if (items.length === 0) return;
    const ctrl = new AbortController();
    // Pequeña espera: al tocar +/+/+ se pide una sola vez.
    const t = setTimeout(async () => {
      setState((s) => ({ ...s, status: "loading" }));
      try {
        const res = await fetch("/api/cotizar", { method: "POST", body: payload, signal: ctrl.signal, headers: { "Content-Type": "application/json" } });
        if (!res.ok) throw new Error(String(res.status));
        setState({ status: "ok", quote: (await res.json()) as QuoteResponse, forPayload: payload });
      } catch (e) {
        if (!ctrl.signal.aborted) setState((s) => ({ ...s, status: "error" }));
        void e;
      }
    }, 250);
    return () => { clearTimeout(t); ctrl.abort(); };
  }, [payload, items.length]);

  if (items.length === 0) {
    return (
      <div className="rounded-xl border border-line bg-white p-6 text-center">
        <p className="text-lg">Tu carrito está vacío.</p>
        <p className="mt-1 text-muted">Busca tus repuestos y agrégalos para enviar una sola cotización por WhatsApp.</p>
        <Link href="/buscar" className="mt-4 inline-flex min-h-12 items-center rounded-lg bg-brand-yellow px-5 font-display text-lg font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark">
          Ver repuestos
        </Link>
      </div>
    );
  }

  const q = state.quote;
  const lineFor = (i: CartItem) => q?.lines.find((l) => l.oem === i.oem && l.code === i.code);
  // Solo es "no existe" si la cotización corresponde al carrito actual (no a uno anterior).
  const isMissing = (i: CartItem) => !!q && state.status === "ok" && state.forPayload === payload && !lineFor(i);

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)] lg:items-start">
      <ul className="flex flex-col gap-3" aria-label="Productos en el carrito">
        {items.map((i) => {
          const line = lineFor(i);
          const missing = isMissing(i);
          return (
            <li key={`${i.oem}|${i.code}`} className="flex flex-col gap-3 rounded-xl border border-line bg-white p-4 sm:flex-row sm:items-center">
              <div className="min-w-0 flex-1">
                <Link href={`/repuesto/${i.slug}`} className="font-semibold underline-offset-2 hover:underline">{i.title}</Link>
                <p className="text-sm text-muted">
                  OEM <span className="font-mono">{i.oem}</span>
                  {line?.showCode && <> · opción <span className="font-mono">{i.code}</span></>}
                  {line && !line.available && <span className="ml-2 text-ask">· Consultar disponibilidad</span>}
                </p>
                {missing && <p className="mt-1 text-sm text-brand-red">Este repuesto ya no está en el catálogo. Quítalo del carrito.</p>}
              </div>
              <div className="flex items-center justify-between gap-3 sm:justify-end">
                {!missing && <QtyStepper value={i.qty} onChange={(n) => cart.setQty(i, n)} label={`Cantidad de ${i.title}`} />}
                <div className="w-28 text-right">
                  {line ? (
                    <>
                      <p className="font-display text-xl font-bold">{money(line.lineTotal)}</p>
                      <p className={`text-sm ${line.mayorApplied ? "font-semibold text-brand-red" : "text-muted"}`}>
                        {money(line.unitPrice)} c/u{line.mayorApplied && " · mayor"}
                      </p>
                    </>
                  ) : !missing && <p className="text-muted">…</p>}
                </div>
                <button
                  type="button"
                  onClick={() => cart.remove(i)}
                  className="h-11 w-11 shrink-0 rounded-lg text-2xl text-muted hover:bg-surface hover:text-brand-ink"
                  aria-label={`Quitar ${i.title} del carrito`}
                >
                  ×
                </button>
              </div>
            </li>
          );
        })}
      </ul>

      <aside className="flex flex-col gap-4 rounded-xl border border-line bg-white p-4 lg:sticky lg:top-4" aria-live="polite">
        {state.status === "error" && (
          <p className="rounded-lg bg-ask-bg px-3 py-2 text-ask">No pudimos calcular los precios. Revisa tu conexión e inténtalo de nuevo.</p>
        )}
        {q && <MayorProgress quote={q} />}
        <div className="flex items-baseline justify-between">
          <span className="font-display text-lg font-bold uppercase tracking-wide">Subtotal</span>
          <span className="font-display text-3xl font-bold">{q ? money(q.total) : "…"}</span>
        </div>
        <p className="-mt-3 text-right text-sm text-muted">IVA incluido</p>
        {q && q.ahorro > 0 && <p className="-mt-3 text-right text-sm font-semibold text-brand-red">Ahorras {money(q.ahorro)}</p>}
        <fieldset className="flex flex-col gap-2">
          <legend className="mb-1 font-display text-lg font-bold uppercase tracking-wide">¿Cómo lo recibes?</legend>
          {DELIVERY_OPTIONS.map((o) => (
            <label key={o.value} className="flex min-h-11 cursor-pointer items-center gap-3 rounded-lg border border-line px-3 has-[:checked]:border-brand-black has-[:checked]:bg-surface">
              <input type="radio" name="entrega" value={o.value} checked={delivery === o.value} onChange={() => deliveryStore.set(o.value)} className="h-5 w-5 accent-brand-black" />
              <span className="flex-1">{o.label}</span>
              <span className="text-sm text-muted">{o.note}</span>
            </label>
          ))}
        </fieldset>
        {q && q.shipping > 0 && (
          <div className="flex items-baseline justify-between border-t border-line pt-3">
            <span>Envío nacional</span><span>{money(q.shipping)}</span>
          </div>
        )}
        {q && q.shipping > 0 && (
          <div className="flex items-baseline justify-between">
            <span className="font-display text-lg font-bold uppercase tracking-wide">Total con envío</span>
            <span className="font-display text-2xl font-bold">{money(q.grandTotal)}</span>
          </div>
        )}
        {vehicle && <p className="text-sm text-muted">Vehículo: <span className="font-semibold text-brand-ink">{vehicleText(vehicle)}</span> (va en el mensaje)</p>}
        {q?.whatsappUrl ? (
          <WhatsAppButton href={q.whatsappUrl} label="Enviar cotización por WhatsApp" full />
        ) : q && (
          <p className="text-sm text-muted">El envío por WhatsApp no está disponible en este momento.</p>
        )}
        <p className="text-sm text-muted">
          Es una cotización: te confirmamos disponibilidad y precio final por WhatsApp. No se cobra nada en la página.
        </p>
        <button type="button" onClick={() => cart.clear()} className="self-start text-sm text-muted underline underline-offset-2 hover:text-brand-ink">
          Vaciar carrito
        </button>
      </aside>
    </div>
  );
}

/**
 * Barra "Te faltan $X para precio al por mayor" (brief §6.4). Solo si hay productos VCH.
 * Regla del cliente: cuentan solo los productos Importadora VCH, sumados a precio por mayor.
 */
function MayorProgress({ quote }: { quote: QuoteResponse }) {
  if (!quote.hasVch) return null;
  const pct = Math.min(100, Math.round((quote.baseMayor / quote.umbral) * 100));
  return (
    <div>
      {quote.mayorApplied ? (
        <p className="font-semibold text-ok">✓ Precio al por mayor aplicado en productos Importadora VCH</p>
      ) : (
        <p>
          Te faltan <strong>{money(quote.faltaParaMayor)}</strong> en productos Importadora VCH para el precio al por mayor.
        </p>
      )}
      <div className="mt-2 h-3 overflow-hidden rounded-full bg-surface" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={pct} aria-label="Avance hacia el precio al por mayor">
        <div className={`h-full rounded-full ${quote.mayorApplied ? "bg-ok" : "bg-brand-yellow"}`} style={{ width: `${pct}%` }} />
      </div>
      <p className="mt-1 text-sm text-muted">
        Llevas {money(quote.baseMayor)} de {money(quote.umbral)}. Se cuentan los productos Importadora VCH a precio por mayor;
        los demás se cobran a precio público.
      </p>
    </div>
  );
}
