import type { Metadata } from "next";
import { connection } from "next/server";
import { CartView } from "@/components/CartView";

export const metadata: Metadata = { title: "Carrito y cotización", robots: { index: false, follow: false } };

export default async function CartPage() {
  // Dinámica: el encabezado (selector de vehículo) se arma con datos vivos de la API, no en la construcción.
  await connection();
  return (
    <div className="flex flex-col gap-5">
      <h1 className="font-display text-3xl font-bold uppercase tracking-wide">Tu cotización</h1>
      <CartView />
    </div>
  );
}
