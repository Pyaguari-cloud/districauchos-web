import { NextResponse } from "next/server";
import type { QuoteResponse } from "@/lib/quote";
import { whatsappNumber } from "@/lib/whatsapp";

/**
 * Puente del carrito a la API .NET (el navegador no habla directo con la API).
 * Recibe solo OEM, variante y cantidad; precios y mensaje los arma el servidor.
 */
const API_URL = process.env.API_URL ?? "http://localhost:8080";
const MAX_BODY = 16 * 1024;

export async function POST(req: Request) {
  const text = await req.text();
  if (text.length > MAX_BODY) return NextResponse.json({ error: "Carrito demasiado grande" }, { status: 413 });

  let body: unknown;
  try {
    body = JSON.parse(text);
  } catch {
    return NextResponse.json({ error: "JSON inválido" }, { status: 400 });
  }
  const b = body as { items?: unknown; vehicle?: unknown; delivery?: unknown };
  const items = Array.isArray(b.items)
    ? b.items.slice(0, 50).map((i) => ({
        oem: String((i as { oem?: unknown }).oem ?? "").slice(0, 64),
        code: String((i as { code?: unknown }).code ?? "").slice(0, 64) || null,
        qty: Number((i as { qty?: unknown }).qty) || 1,
      }))
    : [];
  const vehicle = typeof b.vehicle === "string" ? b.vehicle.slice(0, 80) : null;
  const delivery = typeof b.delivery === "string" && ["pickup", "quito", "national"].includes(b.delivery) ? b.delivery : null;

  const res = await fetch(`${API_URL}/api/quote/preview`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ items, vehicle, delivery }),
    cache: "no-store",
  });
  if (!res.ok) return NextResponse.json({ error: "No se pudo calcular la cotización" }, { status: res.status === 400 ? 400 : 502 });

  const quote = (await res.json()) as Omit<QuoteResponse, "whatsappUrl">;
  const number = whatsappNumber();
  const whatsappUrl = number && quote.lines.length > 0 ? `https://wa.me/${number}?text=${encodeURIComponent(quote.message)}` : null;
  return NextResponse.json({ ...quote, whatsappUrl } satisfies QuoteResponse, { headers: { "Cache-Control": "no-store" } });
}
