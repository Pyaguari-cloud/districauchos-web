"use client";

import { negocio } from "@/content/negocio";
import { createLocalStore } from "./local-store";
import type { Delivery } from "./quote";

/** Texto del costo de envío en el resumen. */
export const shippingLabel = (d: Delivery) => (d === "galapagos" ? "Envío a Galápagos" : "Envío nacional");

/** Forma de entrega elegida en el carrito (se recuerda en este navegador). */
export const deliveryStore = createLocalStore<Delivery>("dc.delivery.v1", "none", (v) =>
  v === "pickup" || v === "quito" || v === "national" || v === "galapagos" ? v : "none",
);

export const DELIVERY_OPTIONS: { value: Exclude<Delivery, "none">; label: string; note: string }[] = [
  { value: "pickup", label: "Retiro en local", note: "Sin costo" },
  { value: "quito", label: "Envío dentro de Quito", note: "Gratis" },
  { value: "national", label: "Envío a provincia", note: `+$${negocio.shipping.national.fee}` },
  { value: "galapagos", label: "Envío a Galápagos", note: `Por avión, +$${negocio.shipping.galapagos.fee}` },
];
