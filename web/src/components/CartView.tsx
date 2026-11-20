"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import type { QuoteResponse } from "@/lib/quote";
import { cart, useCart, type CartItem } from "@/lib/cart";
import { DELIVERY_OPTIONS, deliveryStore } from "@/lib/delivery";
import { isCodeKey, money, partRef } from "@/lib/format";
import { useMyVehicle, vehicleText } from "@/lib/vehicle";
import { QtyStepper } from "./AddToCart";
import { WhatsAppButton } from "./WhatsAppButton";

/** Pago en línea: viene de la API (PAYMENTS_MODE). Apagado = solo cotización por WhatsApp. */
export type PaymentsInfo = { enabled: boolean; backorderDays: number };

type State = { status: "idle" | "loading" | "ok" | "error"; quote?: QuoteResponse; forPayload?: string };

/** Carrito: los precios mostrados son siempre los que calcula el servidor. */
export function CartView({ payments, canceled = false }: { payments: PaymentsInfo; canceled?: boolean }) {
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
                  {partRef(i.oem, i.code).label} <span className="font-mono">{partRef(i.oem, i.code).value}</span>
                  {line?.showCode && !isCodeKey(i.oem) && <> · opción <span className="font-mono">{i.code}</span></>}
                  {line && !line.available && (
                    <span className="ml-2 text-ask">
                      · {payments.enabled && payments.backorderDays > 0 ? `Bajo pedido, llega en ~${payments.backorderDays} días` : "Consultar disponibilidad"}
                    </span>
                  )}
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
        {canceled && (
          <p className="rounded-lg bg-ask-bg px-3 py-2 text-ask">El pago se canceló y no se cobró nada. Puedes intentarlo de nuevo.</p>
        )}
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
        {payments.enabled && q && (() => {
          const noStock = payments.backorderDays === 0 && q.lines.some((l) => !l.available);
          const ready = delivery !== "none" && q.missing.length === 0 && q.lines.length > 0 && !noStock;
          return (
            <>
              {ready ? (
                <Link href="/pagar" className="inline-flex min-h-12 w-full items-center justify-center rounded-lg bg-brand-yellow px-5 font-display text-lg font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark">
                  Pagar en línea · {money(q.grandTotal)}
                </Link>
              ) : (
                <p className="rounded-lg bg-surface px-3 py-2 text-sm">
                  {delivery === "none" ? "Elige cómo lo recibes para pagar en línea." : noStock ? "Hay repuestos sin stock: cotízalos por WhatsApp." : "Quita los repuestos que ya no están para pagar en línea."}
                </p>
              )}
              <p className="-mt-2 text-center text-sm text-muted">Tarjeta de crédito, débito o app PayPhone</p>
              {q.lines.some((l) => !l.available) && payments.backorderDays > 0 && (
                <p className="rounded-lg bg-ask-bg px-3 py-2 text-sm text-ask">
                  Algunos repuestos son <strong>bajo pedido</strong>: llegan en aproximadamente {payments.backorderDays} días.
                </p>
              )}
              <div className="flex items-center gap-3 text-sm text-muted" aria-hidden="true">
                <span className="h-px flex-1 bg-line" />o<span className="h-px flex-1 bg-line" />
              </div>
            </>
          );
        })()}
        {q?.whatsappUrl ? (
          <WhatsAppButton href={q.whatsappUrl} label={payments.enabled ? "Cotizar por WhatsApp" : "Enviar cotización por WhatsApp"} full />
        ) : q && (
          <p className="text-sm text-muted">El envío por WhatsApp no está disponible en este momento.</p>
        )}
        <p className="text-sm text-muted">
          {payments.enabled
            ? "¿Tienes dudas? Cotiza por WhatsApp y te confirmamos disponibilidad y precio antes de pagar."
            : "Es una cotización: te confirmamos disponibilidad y precio final por WhatsApp. No se cobra nada en la página."}
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
