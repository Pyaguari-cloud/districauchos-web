import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { getOrdersConfig } from "@/lib/orders";

export const metadata: Metadata = { title: "Simulador de pago", robots: { index: false, follow: false } };

/** Solo con PAYMENTS_MODE=fake (la API lo permite únicamente en localhost). Imita la página de PayPhone. */
export default async function FakePaymentPage({ searchParams }: { searchParams: Promise<{ tx?: string; monto?: string }> }) {
  const [config, sp] = await Promise.all([getOrdersConfig(), searchParams]);
  if (config.mode !== "fake" || !sp.tx) notFound();
  const tx = encodeURIComponent(sp.tx);
  const amount = (Number(sp.monto) || 0) / 100;
  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-4 rounded-xl border-2 border-dashed border-brand-red bg-white p-6 text-center">
      <p className="font-bold uppercase text-brand-red">Simulador · no es un cobro real</p>
      <h1 className="font-display text-3xl font-bold">Pagar a Districauchos ${amount.toFixed(2)}</h1>
      <Link prefetch={false} href={`/pago/respuesta?id=1&clientTransactionId=${tx}`} className="inline-flex min-h-12 items-center justify-center rounded-lg bg-ok px-5 font-bold text-white">
        Simular pago aprobado
      </Link>
      <Link prefetch={false} href={`/pago/respuesta?id=2&clientTransactionId=${tx}`} className="inline-flex min-h-12 items-center justify-center rounded-lg border border-line px-5 font-bold">
        Simular pago rechazado
      </Link>
      <Link href="/carrito?pago=cancelado" className="text-sm underline">Cerrar (como la X de PayPhone)</Link>
    </div>
  );
}
