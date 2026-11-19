import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { CheckoutForm } from "@/components/CheckoutForm";
import { getOrdersConfig } from "@/lib/orders";

export const metadata: Metadata = { title: "Pagar pedido", robots: { index: false, follow: false } };

export default async function PayPage() {
  const config = await getOrdersConfig();
  if (!config.enabled) notFound();
  return (
    <div className="flex flex-col gap-5">
      <h1 className="font-display text-3xl font-bold uppercase tracking-wide">Pagar pedido</h1>
      <CheckoutForm backorderDays={config.backorderDays} consumidorFinalMax={config.consumidorFinalMax} />
    </div>
  );
}
