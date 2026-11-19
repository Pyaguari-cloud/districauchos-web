import type { Metadata } from "next";
import Link from "next/link";
import { ClearCart } from "@/components/ClearCart";
import { WhatsAppButton } from "@/components/WhatsAppButton";
import { money } from "@/lib/format";
import { confirmOrder } from "@/lib/orders";
import { whatsappNumber } from "@/lib/whatsapp";

export const metadata: Metadata = { title: "Estado del pago", robots: { index: false, follow: false } };

type Params = Promise<{ id?: string; clientTransactionId?: string }>;

/**
 * PayPhone devuelve aquí al comprador. El pago se verifica en el servidor con PayPhone
 * (hay 5 minutos para confirmarlo; si no, PayPhone lo reversa solo).
 */
export default async function PaymentResultPage({ searchParams }: { searchParams: Params }) {
  const sp = await searchParams;
  const id = Number(sp.id);
  const tx = sp.clientTransactionId ?? "";
  const result = Number.isSafeInteger(id) && id > 0 && tx ? await confirmOrder(id, tx) : null;

  if (result === "retry") {
    return (
      <Box title="Estamos confirmando tu pago">
        <p>PayPhone está tardando en responder. <strong>Recarga esta página</strong> en unos segundos; no vuelvas a pagar.</p>
      </Box>
    );
  }
  if (!result) {
    return (
      <Box title="No encontramos este pago">
        <p>Si se te cobró, escríbenos por WhatsApp con el comprobante de PayPhone y lo revisamos.</p>
        <Link href="/carrito" className="font-semibold underline underline-offset-2">Volver al carrito</Link>
      </Box>
    );
  }

  if (result.status === "paid" || result.status === "review") {
    const number = whatsappNumber();
    const wa = number ? `https://wa.me/${number}?text=${encodeURIComponent(result.whatsappText)}` : null;
    return (
      <Box title={`¡Pago recibido! Pedido #${result.number}`} ok>
        <ClearCart />
        <p>
          Gracias, {result.name.split(" ")[0]}. Recibimos tu pago de <strong>{money(result.total)}</strong>. Te enviaremos la factura
          a <strong>{result.email}</strong>.
        </p>
        <ul className="flex flex-col gap-1 rounded-lg bg-surface p-3 text-sm">
          {result.lines.map((l) => (
            <li key={l.oem + l.title} className="flex justify-between gap-3">
              <span>{l.qty} × {l.title}{l.backorder && <span className="text-ask"> · bajo pedido (~{result.backorderDays} días)</span>}</span>
              <span className="shrink-0">{money(l.lineTotal)}</span>
            </li>
          ))}
          {result.shipping > 0 && <li className="flex justify-between"><span>Envío</span><span>{money(result.shipping)}</span></li>}
        </ul>
        <p><strong>Entrega:</strong> {result.delivery}</p>
        {wa && (
          <>
            <p>Para agilizar el despacho, avísanos por WhatsApp:</p>
            <WhatsAppButton href={wa} label={`Avisar pedido #${result.number}`} full />
          </>
        )}
        <Link href="/buscar" className="self-start font-semibold underline underline-offset-2">Seguir viendo repuestos</Link>
      </Box>
    );
  }

  return (
    <Box title={result.status === "canceled" ? "El pago se canceló" : "El pago no se completó"}>
      <p>No se realizó ningún cobro. Tu carrito sigue guardado; puedes intentarlo de nuevo o cotizar por WhatsApp.</p>
      <Link href="/carrito" className="inline-flex min-h-12 items-center justify-center self-start rounded-lg bg-brand-yellow px-5 font-display text-lg font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark">
        Volver al carrito
      </Link>
    </Box>
  );
}

function Box({ title, ok = false, children }: { title: string; ok?: boolean; children: React.ReactNode }) {
  return (
    <div className={`mx-auto flex w-full max-w-2xl flex-col gap-4 rounded-xl border bg-white p-6 ${ok ? "border-ok" : "border-line"}`}>
      <h1 className={`font-display text-3xl font-bold uppercase tracking-wide ${ok ? "text-ok" : ""}`}>{title}</h1>
      {children}
    </div>
  );
}
