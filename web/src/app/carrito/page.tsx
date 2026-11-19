import type { Metadata } from "next";
import { connection } from "next/server";
import { CartView } from "@/components/CartView";
import { getOrdersConfig } from "@/lib/orders";

export const metadata: Metadata = { title: "Carrito y cotización", robots: { index: false, follow: false } };

export default async function CartPage({ searchParams }: { searchParams: Promise<{ pago?: string }> }) {
  // Dinámica: el encabezado (selector de vehículo) se arma con datos vivos de la API, no en la construcción.
  await connection();
  const [config, sp] = await Promise.all([getOrdersConfig(), searchParams]);
  return (
    <div className="flex flex-col gap-5">
      <h1 className="font-display text-3xl font-bold uppercase tracking-wide">{config.enabled ? "Tu carrito" : "Tu cotización"}</h1>
      <CartView payments={{ enabled: config.enabled, backorderDays: config.backorderDays }} canceled={sp.pago === "cancelado"} />
    </div>
  );
}
