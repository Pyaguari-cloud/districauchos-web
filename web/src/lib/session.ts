import "server-only";
import { cookies } from "next/headers";
import { NextResponse } from "next/server";

/**
 * Sesión de las cuentas de clientes. El token vive en una cookie httpOnly (el JavaScript de la página no
 * puede leerlo) y se reenvía a la API .NET como "Bearer". Aparte va DC_NAME, una cookie legible solo con el
 * primer nombre, para que el encabezado diga "Hola, Juan" sin volver dinámica cada página.
 */
const API_URL = process.env.API_URL ?? "http://localhost:8080";
export const SESSION_COOKIE = "dc_session";
export const NAME_COOKIE = "dc_name";
const MAX_AGE = 30 * 24 * 3600; // igual que la sesión en la API
const secure = process.env.NODE_ENV === "production";

export async function sessionToken(): Promise<string | null> {
  const t = (await cookies()).get(SESSION_COOKIE)?.value;
  return t && /^[A-Za-z0-9_-]{40,64}$/.test(t) ? t : null;
}

/** IP real del visitante (Cloudflare → Caddy → web) para que la API limite intentos por persona. */
export function clientIp(req: Request): string {
  const h = req.headers;
  return (h.get("cf-connecting-ip") ?? h.get("x-forwarded-for")?.split(",")[0] ?? h.get("x-real-ip") ?? "").trim().slice(0, 64);
}

/**
 * Las rutas que cambian algo solo aceptan pedidos desde la misma página (defensa extra contra CSRF,
 * además de SameSite=Lax en la cookie).
 */
export function sameOrigin(req: Request): boolean {
  const origin = req.headers.get("origin");
  if (!origin) return false;
  const host = req.headers.get("x-forwarded-host") ?? req.headers.get("host");
  try {
    return new URL(origin).host === host;
  } catch {
    return false;
  }
}

/** Llama a la API de cuentas con la sesión del visitante (si tiene). */
export async function accountApi(path: string, init: { method?: string; body?: unknown; req?: Request; token?: string | null } = {}) {
  const token = init.token === undefined ? await sessionToken() : init.token;
  return fetch(`${API_URL}/api/account${path}`, {
    method: init.method ?? "GET",
    headers: {
      ...(init.body !== undefined ? { "Content-Type": "application/json" } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init.req ? { "X-Client-IP": clientIp(init.req) } : {}),
    },
    body: init.body !== undefined ? JSON.stringify(init.body) : undefined,
    cache: "no-store",
  });
}

const firstName = (name: string) => name.trim().split(/\s+/)[0]?.slice(0, 30) ?? "";

export function setSession(res: NextResponse, token: string, name: string) {
  res.cookies.set(SESSION_COOKIE, token, { httpOnly: true, secure, sameSite: "lax", path: "/", maxAge: MAX_AGE });
  setName(res, name);
}

export function setName(res: NextResponse, name: string) {
  res.cookies.set(NAME_COOKIE, encodeURIComponent(firstName(name)), { httpOnly: false, secure, sameSite: "lax", path: "/", maxAge: MAX_AGE });
}

export function clearSession(res: NextResponse) {
  res.cookies.set(SESSION_COOKIE, "", { httpOnly: true, secure, sameSite: "lax", path: "/", maxAge: 0 });
  res.cookies.set(NAME_COOKIE, "", { httpOnly: false, secure, sameSite: "lax", path: "/", maxAge: 0 });
}

export type Me = {
  email: string;
  name: string;
  profile: { phone?: string | null; docType?: string | null; docNumber?: string | null; address?: string | null; city?: string | null };
  garage: unknown[];
  createdAt: string;
};

export type AccountOrder = { number: number; status: string; createdAt: string; total: number; delivery: string; items: number; summary: string };

/** Cuenta del visitante para páginas del servidor, o null si no hay sesión válida. */
export async function getMe(): Promise<Me | null> {
  if (!(await sessionToken())) return null;
  try {
    const res = await accountApi("/me");
    return res.ok ? ((await res.json()) as Me) : null;
  } catch {
    return null;
  }
}

export async function getMyOrders(): Promise<AccountOrder[]> {
  try {
    const res = await accountApi("/orders");
    return res.ok ? ((await res.json()) as AccountOrder[]) : [];
  } catch {
    return [];
  }
}
