"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useCart } from "@/lib/cart";
import { DELIVERY_OPTIONS, deliveryStore } from "@/lib/delivery";
import { money } from "@/lib/format";
import type { QuoteResponse } from "@/lib/quote";
import { useMyVehicle, vehicleText } from "@/lib/vehicle";

type Fields = Record<"name" | "docType" | "docNumber" | "phone" | "email" | "address" | "city" | "notes", string>;
const EMPTY: Fields = { name: "", docType: "cedula", docNumber: "", phone: "", email: "", address: "", city: "", notes: "" };

const DOC_TYPES = [
  { value: "cedula", label: "Cédula" },
  { value: "ruc", label: "RUC" },
  { value: "pasaporte", label: "Pasaporte" },
];

/**
 * Datos de entrega y facturación, y paso a PayPhone. Los precios los recalcula el servidor:
 * lo que se muestra aquí es solo el resumen.
 */
export function CheckoutForm({ backorderDays }: { backorderDays: number }) {
  const items = useCart();
  const delivery = deliveryStore.use();
  const vehicle = useMyVehicle();
  const [quote, setQuote] = useState<QuoteResponse | null>(null);
  const [f, setF] = useState<Fields>(EMPTY);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [acceptBackorder, setAcceptBackorder] = useState(false);
  const [sending, setSending] = useState(false);

  const payloadItems = items.map(({ oem, code, qty }) => ({ oem, code, qty }));
  const itemsKey = JSON.stringify(payloadItems);

  useEffect(() => {
    if (payloadItems.length === 0 || delivery === "none") return;
    const ctrl = new AbortController();
    fetch("/api/cotizar", {
      method: "POST", signal: ctrl.signal, headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ items: payloadItems, delivery }),
    })
      .then((r) => (r.ok ? r.json() : null))
      .then((q: QuoteResponse | null) => setQuote(q))
      .catch(() => { /* se reintenta al cambiar algo */ });
    return () => ctrl.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- itemsKey resume payloadItems
  }, [itemsKey, delivery]);

  if (items.length === 0 || delivery === "none") {
    return (
      <div className="rounded-xl border border-line bg-white p-6 text-center">
        <p className="text-lg">{items.length === 0 ? "Tu carrito está vacío." : "Primero elige cómo recibes tu pedido."}</p>
        <Link href="/carrito" className="mt-4 inline-flex min-h-12 items-center rounded-lg bg-brand-yellow px-5 font-display text-lg font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark">
          Ir al carrito
        </Link>
      </div>
    );
  }

  const hasBackorder = !!quote?.lines.some((l) => !l.available);
  const needsAddress = delivery === "quito" || delivery === "national";
  const deliveryLabel = DELIVERY_OPTIONS.find((o) => o.value === delivery)?.label;

  const set = (k: keyof Fields) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
    setF((s) => ({ ...s, [k]: e.target.value }));

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (sending) return;
    if (hasBackorder && !acceptBackorder) {
      setFormError(`Confirma que aceptas el tiempo de entrega de los repuestos bajo pedido (~${backorderDays} días).`);
      return;
    }
    setSending(true);
    setErrors({});
    setFormError(null);
    try {
      const res = await fetch("/api/pedido", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          items: payloadItems, delivery, vehicle: vehicleText(vehicle) || null,
          customer: f,
        }),
      });
      const data = (await res.json().catch(() => ({}))) as { payUrl?: string; error?: string; fields?: Record<string, string> };
      if (res.ok && data.payUrl) {
        window.location.assign(data.payUrl); // página de pago de PayPhone
        return;
      }
      setErrors(data.fields ?? {});
      setFormError(data.error ?? "No pudimos procesar el pedido. Inténtalo de nuevo.");
    } catch {
      setFormError("Revisa tu conexión e inténtalo de nuevo.");
    }
    setSending(false);
  }

  const field = (k: keyof Fields, label: string, props: React.InputHTMLAttributes<HTMLInputElement> = {}) => (
    <div className="flex flex-col gap-1">
      <label htmlFor={`f-${k}`} className="font-semibold">{label}</label>
      <input
        id={`f-${k}`} name={k} value={f[k]} onChange={set(k)}
        aria-invalid={!!errors[k]} aria-describedby={errors[k] ? `e-${k}` : undefined}
        className={`min-h-12 rounded-lg border bg-white px-3 text-base ${errors[k] ? "border-brand-red" : "border-line"}`}
        {...props}
      />
      {errors[k] && <p id={`e-${k}`} className="text-sm text-brand-red">{errors[k]}</p>}
    </div>
  );

  return (
    <form onSubmit={submit} noValidate className="grid gap-6 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)] lg:items-start">
      <div className="flex flex-col gap-6">
        <fieldset className="flex flex-col gap-4 rounded-xl border border-line bg-white p-4">
          <legend className="px-1 font-display text-xl font-bold uppercase tracking-wide">Tus datos</legend>
          {field("name", "Nombre y apellido", { autoComplete: "name", required: true })}
          <div className="grid gap-4 sm:grid-cols-2">
            {field("phone", "Celular", { autoComplete: "tel", inputMode: "tel", placeholder: "0987654321", required: true })}
            {field("email", "Correo", { autoComplete: "email", inputMode: "email", type: "email", required: true })}
          </div>
        </fieldset>

        <fieldset className="flex flex-col gap-4 rounded-xl border border-line bg-white p-4">
          <legend className="px-1 font-display text-xl font-bold uppercase tracking-wide">Factura</legend>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-1">
              <label htmlFor="f-docType" className="font-semibold">Documento</label>
              <select
                id="f-docType" value={f.docType} onChange={set("docType")}
                aria-invalid={!!errors.docType} aria-describedby={errors.docType ? "e-docType" : undefined}
                className={`min-h-12 rounded-lg border bg-white px-3 text-base ${errors.docType ? "border-brand-red" : "border-line"}`}
              >
                {DOC_TYPES.map((d) => (
                  <option key={d.value} value={d.value}>{d.label}</option>
                ))}
              </select>
              {errors.docType && <p id="e-docType" className="text-sm text-brand-red">{errors.docType}</p>}
            </div>
            {field("docNumber", f.docType === "ruc" ? "Número de RUC" : f.docType === "pasaporte" ? "Número de pasaporte" : "Número de cédula", {
                inputMode: f.docType === "pasaporte" ? "text" : "numeric", autoComplete: "off", required: true,
              })}
          </div>
          <p className="text-sm text-muted">Te enviamos la factura electrónica a tu correo.</p>
        </fieldset>

        <fieldset className="flex flex-col gap-4 rounded-xl border border-line bg-white p-4">
          <legend className="px-1 font-display text-xl font-bold uppercase tracking-wide">Entrega: {deliveryLabel}</legend>
          {needsAddress ? (
            <>
              {delivery === "national" && field("city", "Ciudad de destino", { autoComplete: "address-level2", required: true })}
              {field("address", delivery === "national" ? "Dirección o cooperativa de preferencia" : "Dirección de entrega en Quito", {
                autoComplete: "street-address", required: true,
              })}
            </>
          ) : (
            <p className="text-muted">Te avisamos por WhatsApp cuando tu pedido esté listo para retirar.</p>
          )}
          <div className="flex flex-col gap-1">
            <label htmlFor="f-notes" className="font-semibold">Nota (opcional)</label>
            <textarea id="f-notes" value={f.notes} onChange={set("notes")} maxLength={300} rows={2}
              className="rounded-lg border border-line bg-white px-3 py-2 text-base" placeholder="Referencia, horario, local donde retiras…" />
          </div>
          <Link href="/carrito" className="self-start text-sm underline underline-offset-2">Cambiar forma de entrega</Link>
        </fieldset>
      </div>

      <aside className="flex flex-col gap-4 rounded-xl border border-line bg-white p-4 lg:sticky lg:top-4" aria-live="polite">
        <h2 className="font-display text-xl font-bold uppercase tracking-wide">Tu pedido</h2>
        {quote ? (
          <ul className="flex flex-col gap-2 text-sm">
            {quote.lines.map((l) => (
              <li key={`${l.oem}|${l.code}`} className="flex justify-between gap-3">
                <span className="min-w-0">
                  {l.qty} × {l.title}
                  {!l.available && <span className="block text-ask">Bajo pedido, ~{backorderDays} días</span>}
                </span>
                <span className="shrink-0 font-semibold">{money(l.lineTotal)}</span>
              </li>
            ))}
          </ul>
        ) : <p className="text-muted">Calculando…</p>}
        {quote && (
          <div className="flex flex-col gap-1 border-t border-line pt-3">
            {quote.mayorApplied && <p className="text-sm font-semibold text-ok">✓ Precio al por mayor aplicado</p>}
            {quote.shipping > 0 && (
              <p className="flex justify-between"><span>Envío nacional</span><span>{money(quote.shipping)}</span></p>
            )}
            <p className="flex items-baseline justify-between">
              <span className="font-display text-lg font-bold uppercase tracking-wide">Total</span>
              <span className="font-display text-3xl font-bold">{money(quote.grandTotal)}</span>
            </p>
            <p className="text-right text-sm text-muted">IVA incluido</p>
          </div>
        )}
        {hasBackorder && (
          <label className="flex cursor-pointer gap-3 rounded-lg bg-ask-bg p-3 text-sm text-ask">
            <input type="checkbox" checked={acceptBackorder} onChange={(e) => setAcceptBackorder(e.target.checked)} className="mt-0.5 h-5 w-5 shrink-0" />
            <span>Entiendo que los repuestos <strong>bajo pedido</strong> llegan en aproximadamente {backorderDays} días.</span>
          </label>
        )}
        {formError && <p role="alert" className="rounded-lg bg-brand-red/10 px-3 py-2 text-brand-red">{formError}</p>}
        <button
          type="submit" disabled={sending || !quote || quote.missing.length > 0}
          className="inline-flex min-h-12 w-full items-center justify-center rounded-lg bg-brand-yellow px-5 font-display text-lg font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark disabled:cursor-not-allowed disabled:opacity-60"
        >
          {sending ? "Conectando con PayPhone…" : `Pagar ${quote ? money(quote.grandTotal) : ""} con PayPhone`}
        </button>
        <p className="text-sm text-muted">
          Pagas en la página segura de PayPhone con tarjeta de crédito, débito o la app PayPhone. Nosotros no vemos ni
          guardamos los datos de tu tarjeta. Usamos tus datos solo para facturar y entregar tu pedido.
        </p>
      </aside>
    </form>
  );
}
