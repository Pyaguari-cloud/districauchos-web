"use client";

import { useSyncExternalStore } from "react";

/**
 * Estado de la cuenta en el navegador. La sesión real está en una cookie httpOnly que este código no ve;
 * aquí solo se lee "dc_name" (el primer nombre) para saber si hay alguien dentro y saludarlo.
 */
const EVENT = "dc-account-change";

function readName(): string | null {
  const m = document.cookie.match(/(?:^|;\s*)dc_name=([^;]*)/);
  if (!m || !m[1]) return null;
  try {
    return decodeURIComponent(decodeURIComponent(m[1])) || null;
  } catch {
    return null;
  }
}

function subscribe(onChange: () => void) {
  window.addEventListener(EVENT, onChange);
  window.addEventListener("focus", onChange); // otra pestaña pudo entrar o salir
  return () => {
    window.removeEventListener(EVENT, onChange);
    window.removeEventListener("focus", onChange);
  };
}

/** Primer nombre de quien entró, o null. En el servidor y en la primera pintura: null. */
export const useAccountName = () => useSyncExternalStore(subscribe, readName, () => null);

/** Avisar después de entrar, salir o cambiar el nombre. */
export const accountChanged = () => window.dispatchEvent(new Event(EVENT));

export type ApiResult<T = Record<string, unknown>> = { ok: boolean; status: number; data: T & { error?: string; fields?: Record<string, string> } };

/** Llama a /api/cuenta/<accion> y devuelve el resultado ya leído (nunca lanza). */
export async function cuenta<T = Record<string, unknown>>(accion: string, method: "GET" | "POST" | "PUT" = "POST", body?: unknown): Promise<ApiResult<T>> {
  try {
    const res = await fetch(`/api/cuenta/${accion}`, {
      method,
      headers: body !== undefined ? { "Content-Type": "application/json" } : undefined,
      body: body !== undefined ? JSON.stringify(body) : undefined,
      cache: "no-store",
    });
    const data = (await res.json().catch(() => ({}))) as ApiResult<T>["data"];
    return { ok: res.ok, status: res.status, data };
  } catch {
    return { ok: false, status: 0, data: { error: "Revisa tu conexión e inténtalo de nuevo." } as ApiResult<T>["data"] };
  }
}
