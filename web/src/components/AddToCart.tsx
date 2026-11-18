"use client";

import Link from "next/link";
import { useState } from "react";
import { cart, MAX_QTY, type CartItem } from "@/lib/cart";

type Props = Omit<CartItem, "qty"> & { compact?: boolean };

/** Agregar al carrito con cantidad. Versión compacta para la tabla de opciones. */
export function AddToCart({ compact = false, ...item }: Props) {
  const [qty, setQty] = useState(1);
  const [added, setAdded] = useState(false);

  function add() {
    cart.add({ ...item, qty });
    setAdded(true);
  }

  if (compact) {
    return (
      <button
        type="button"
        onClick={add}
        className="min-h-11 rounded-lg border-2 border-brand-black px-3 font-display text-sm font-bold uppercase tracking-wide hover:bg-brand-yellow"
        aria-label={`Agregar opción ${item.code} al carrito`}
      >
        {added ? "Agregado ✓" : "Agregar"}
      </button>
    );
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex gap-2">
        <QtyStepper value={qty} onChange={setQty} label={`Cantidad de ${item.title}`} />
        <button
          type="button"
          onClick={add}
          className="min-h-12 flex-1 rounded-lg bg-brand-yellow px-4 font-display text-lg font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark"
        >
          Agregar al carrito
        </button>
      </div>
      <p aria-live="polite" className="min-h-6 text-sm">
        {added && (
          <>
            Agregado al carrito.{" "}
            <Link href="/carrito" className="font-semibold underline underline-offset-2">Ver carrito y cotizar</Link>
          </>
        )}
      </p>
    </div>
  );
}

export function QtyStepper({ value, onChange, label }: { value: number; onChange: (n: number) => void; label: string }) {
  const set = (n: number) => onChange(Math.min(MAX_QTY, Math.max(1, Number.isFinite(n) ? Math.round(n) : 1)));
  const btn = "h-12 w-11 text-xl font-bold hover:bg-surface disabled:opacity-40";
  return (
    <div className="inline-flex shrink-0 items-center rounded-lg border-2 border-line bg-white" role="group" aria-label={label}>
      <button type="button" className={btn} onClick={() => set(value - 1)} disabled={value <= 1} aria-label="Quitar uno">−</button>
      <input
        type="number"
        inputMode="numeric"
        min={1}
        max={MAX_QTY}
        value={value}
        onChange={(e) => set(Number(e.target.value))}
        aria-label="Cantidad"
        className="h-12 w-12 border-x border-line text-center text-lg [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none"
      />
      <button type="button" className={btn} onClick={() => set(value + 1)} disabled={value >= MAX_QTY} aria-label="Agregar uno">+</button>
    </div>
  );
}
