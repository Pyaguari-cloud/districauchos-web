import { NextResponse } from "next/server";

/**
 * Puente del formulario de pago a la API .NET. La API recalcula precios y stock, valida los datos
 * y prepara el cobro en PayPhone; aquí solo se recortan tamaños.
 */
const API_URL = process.env.API_URL ?? "http://localhost:8080";
const MAX_BODY = 24 * 1024;

const str = (v: unknown, max: number) => (typeof v === "string" ? v.slice(0, max) : null);

export async function POST(req: Request) {
  const text = await req.text();
  if (text.length > MAX_BODY) return NextResponse.json({ error: "Pedido demasiado grande" }, { status: 413 });
  let b: { items?: unknown; vehicle?: unknown; delivery?: unknown; customer?: Record<string, unknown> };
  try {
    b = JSON.parse(text);
  } catch {
    return NextResponse.json({ error: "JSON inválido" }, { status: 400 });
  }
  const items = Array.isArray(b.items)
    ? b.items.slice(0, 50).map((i) => ({
        oem: str((i as { oem?: unknown }).oem, 64) ?? "",
        code: str((i as { code?: unknown }).code, 64) || null,
        qty: Number((i as { qty?: unknown }).qty) || 1,
      }))
    : [];
  const c = b.customer ?? {};
  const customer = {
    name: str(c.name, 100), docType: str(c.docType, 20), docNumber: str(c.docNumber, 20), phone: str(c.phone, 20),
    email: str(c.email, 120), address: str(c.address, 200), city: str(c.city, 60), notes: str(c.notes, 300),
  };

  let res: Response;
  try {
    res = await fetch(`${API_URL}/api/orders`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ items, vehicle: str(b.vehicle, 80), delivery: str(b.delivery, 20), customer }),
      cache: "no-store",
    });
  } catch {
    return NextResponse.json({ error: "No pudimos procesar el pedido. Inténtalo de nuevo." }, { status: 502 });
  }
  if (res.status === 404) return NextResponse.json({ error: "El pago en línea no está disponible." }, { status: 404 });
  const data = (await res.json().catch(() => ({}))) as { payUrl?: string; error?: string; fields?: Record<string, string> };
  if (res.ok && typeof data.payUrl === "string" && /^https?:\/\//.test(data.payUrl)) {
    return NextResponse.json({ payUrl: data.payUrl }, { headers: { "Cache-Control": "no-store" } });
  }
  return NextResponse.json({ error: data.error ?? "No pudimos procesar el pedido.", fields: data.fields ?? {} }, { status: res.ok ? 502 : res.status });
}
