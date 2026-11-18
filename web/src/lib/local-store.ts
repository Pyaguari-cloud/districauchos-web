"use client";

import { useSyncExternalStore } from "react";

/**
 * Valor guardado en localStorage y compartido entre componentes y pestañas.
 * Si el navegador bloquea el almacenamiento (modo privado), funciona en memoria.
 */
export function createLocalStore<T>(key: string, fallback: T, validate: (v: unknown) => T) {
  const EVENT = `local-store:${key}`;
  let memory: T = fallback;
  let cachedRaw: string | null | undefined;
  let cachedValue: T = fallback;

  function read(): T {
    let raw: string | null = null;
    try {
      raw = window.localStorage.getItem(key);
    } catch {
      return memory;
    }
    if (raw === cachedRaw) return cachedValue; // misma referencia mientras no cambie (requisito de useSyncExternalStore)
    cachedRaw = raw;
    try {
      cachedValue = raw === null ? fallback : validate(JSON.parse(raw));
    } catch {
      cachedValue = fallback;
    }
    return cachedValue;
  }

  function write(value: T) {
    memory = value;
    try {
      window.localStorage.setItem(key, JSON.stringify(value));
    } catch {
      /* sin almacenamiento: queda en memoria */
    }
    window.dispatchEvent(new Event(EVENT));
  }

  function subscribe(onChange: () => void) {
    const onStorage = (e: StorageEvent) => e.key === key && onChange();
    window.addEventListener(EVENT, onChange);
    window.addEventListener("storage", onStorage); // otras pestañas
    return () => {
      window.removeEventListener(EVENT, onChange);
      window.removeEventListener("storage", onStorage);
    };
  }

  return {
    get: read,
    set: (update: T | ((prev: T) => T)) =>
      write(typeof update === "function" ? (update as (prev: T) => T)(read()) : update),
    // En el servidor y en la primera pintura se usa el valor por defecto (evita errores de hidratación).
    use: () => useSyncExternalStore(subscribe, read, () => fallback),
  };
}
