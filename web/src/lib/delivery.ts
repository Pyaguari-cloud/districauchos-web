"use client";

import { negocio } from "@/content/negocio";
import { createLocalStore } from "./local-store";
import type { Delivery } from "./quote";

/** Forma de entrega elegida en el carrito (se recuerda en este navegador). */
export const deliveryStore = createLocalStore<Delivery>("dc.delivery.v1", "none", (v) =>
  v === "pickup" || v === "quito" || v === "national" ? v : "none",
);

export const DELIVERY_OPTIONS: { value: Exclude<Delivery, "none">; label: string; note: string }[] = [
  { value: "pickup", label: "Retiro en local", note: "Sin costo" },
  { value: "quito", label: "Envío dentro de Quito", note: "Gratis" },
  { value: "national", label: "Envío a provincia", note: `Por cooperativa, +$${negocio.shipping.national.fee}` },
];
