import { NextResponse } from "next/server";
import { clientIp, sameOrigin, sessionToken } from "@/lib/session";

/**
 * Búsqueda por placa: puente hacia la API .NET, que guarda cada placa (no se paga dos veces) y pone los límites.
 * Solo desde nuestra propia página (Origin), para que nadie use la ruta como servicio gratis de placas.
 * La sesión va como Bearer: las cuentas de los vendedores tienen un límite más alto (todos salen por la IP del local).
 */
const API_URL = process.env.API_URL ?? "http://localhost:8080";
const noStore = { "Cache-Control": "no-store" };

export async function POST(req: Request) {
  if (!sameOrigin(req)) return NextResponse.json({ error: "Pedido no permitido" }, { status: 403 });
  const body = (await req.json().catch(() => null)) as { placa?: unknown } | null;
  const placa = typeof body?.placa === "string" ? body.placa.trim().slice(0, 20) : "";
  if (!placa) return NextResponse.json({ error: "Escribe la placa." }, { status: 400, headers: noStore });
  try {
    const token = await sessionToken();
    const res = await fetch(`${API_URL}/api/plate/${encodeURIComponent(placa)}`, {
      headers: { "X-Client-IP": clientIp(req), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      cache: "no-store",
      signal: AbortSignal.timeout(30_000),
    });
    const data = (await res.json().catch(() => ({}))) as Record<string, unknown>;
    if (!res.ok) {
      const error = typeof data.error === "string" ? data.error : "No pudimos consultar la placa. Inténtalo de nuevo.";
      return NextResponse.json({ error }, { status: res.status, headers: noStore });
    }
    return NextResponse.json(data, { headers: noStore });
  } catch {
    return NextResponse.json({ error: "No pudimos consultar la placa en este momento. Inténtalo en unos minutos." }, { status: 502, headers: noStore });
  }
}
