"use client";

import { createLocalStore } from "./local-store";

/**
 * Carrito en localStorage (brief §6.7). Título y precio son solo para mostrar mientras carga;
 * los precios reales los calcula el servidor en /api/cotizar.
 */
export type CartItem = { oem: string; code: string; qty: number; slug: string; title: string; price: number };

export const MAX_QTY = 999;
const MAX_LINES = 50;

const isItem = (x: unknown): x is CartItem => {
  const i = x as CartItem;
  return !!i && typeof i.oem === "string" && typeof i.code === "string" && Number.isInteger(i.qty) && i.qty > 0
    && typeof i.slug === "string" && typeof i.title === "string" && typeof i.price === "number";
};

const store = createLocalStore<CartItem[]>("dc.cart.v1", [], (v) =>
  Array.isArray(v) ? v.filter(isItem).slice(0, MAX_LINES) : [],
);

const same = (a: Pick<CartItem, "oem" | "code">, b: Pick<CartItem, "oem" | "code">) => a.oem === b.oem && a.code === b.code;
const clamp = (n: number) => Math.min(MAX_QTY, Math.max(1, Math.round(n)));

export const useCart = store.use;

export const cart = {
  add(item: CartItem) {
    store.set((items) => {
      const existing = items.find((i) => same(i, item));
      if (existing) return items.map((i) => (same(i, item) ? { ...item, qty: clamp(i.qty + item.qty) } : i));
      return [...items, { ...item, qty: clamp(item.qty) }].slice(0, MAX_LINES);
    });
  },
  setQty(key: Pick<CartItem, "oem" | "code">, qty: number) {
    store.set((items) => items.map((i) => (same(i, key) ? { ...i, qty: clamp(qty) } : i)));
  },
  remove(key: Pick<CartItem, "oem" | "code">) {
    store.set((items) => items.filter((i) => !same(i, key)));
  },
  clear() {
    store.set([]);
  },
};
