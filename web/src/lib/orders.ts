import "server-only";

/** Pagos en línea (PayPhone). La API decide si están activos (PAYMENTS_MODE). */
const API_URL = process.env.API_URL ?? "http://localhost:8080";

export type OrdersConfig = { enabled: boolean; mode: "off" | "payphone" | "fake"; backorderDays: number };

export type OrderSummary = {
  number: number;
  status: "pending" | "paid" | "canceled" | "failed" | "review";
  subtotal: number; shipping: number; total: number;
  delivery: string; backorderDays: number; hasBackorder: boolean;
  lines: { oem: string; title: string; qty: number; lineTotal: number; backorder: boolean }[];
  name: string; email: string; whatsappText: string;
};

const OFF: OrdersConfig = { enabled: false, mode: "off", backorderDays: 0 };

export async function getOrdersConfig(): Promise<OrdersConfig> {
  try {
    const res = await fetch(`${API_URL}/api/orders/config`, { cache: "no-store" });
    return res.ok ? ((await res.json()) as OrdersConfig) : OFF;
  } catch {
    return OFF; // si la API no responde, se sigue cotizando por WhatsApp
  }
}

/** Verifica el pago con PayPhone (lo hace la API). null = pedido inexistente; "retry" = PayPhone no respondió. */
export async function confirmOrder(id: number, clientTransactionId: string): Promise<OrderSummary | null | "retry"> {
  const res = await fetch(`${API_URL}/api/orders/confirm`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ id, clientTransactionId }),
    cache: "no-store",
  });
  if (res.status === 404) return null;
  if (!res.ok) return "retry";
  return (await res.json()) as OrderSummary;
}
