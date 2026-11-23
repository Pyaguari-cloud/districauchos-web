"use client";

import { useEffect, useRef } from "react";
import { cuenta, useAccountName } from "@/lib/account";
import { garage, useGarage } from "@/lib/vehicle";

/**
 * Con cuenta, "Mi garaje" se guarda también en el servidor: al entrar se juntan los vehículos de la cuenta
 * con los de este navegador, y cada cambio se sube. Sin cuenta no hace nada (todo sigue en el navegador).
 */
export function GarageSync() {
  const name = useAccountName();
  const { vehicles } = useGarage();
  const ready = useRef(false);
  const lastSent = useRef<string>("");

  /** Sube el garaje de este navegador si es distinto de lo que tiene la cuenta. */
  const push = () => {
    const payload = garage.vehicles();
    const text = JSON.stringify(payload);
    if (text === lastSent.current) return;
    lastSent.current = text;
    void cuenta("garaje", "PUT", payload);
  };

  // Al entrar (o al abrir la página con sesión): traer y juntar.
  useEffect(() => {
    ready.current = false;
    if (!name) return;
    let cancelled = false;
    cuenta<unknown[]>("garaje", "GET").then((r) => {
      if (cancelled || !r.ok || !Array.isArray(r.data)) return;
      lastSent.current = JSON.stringify(r.data);
      garage.merge(r.data);
      ready.current = true;
      push(); // si este navegador tenía vehículos que la cuenta no, se suben ya
    });
    return () => { cancelled = true; };
  }, [name]);

  // Cada cambio del garaje (también el de juntar): subirlo si es distinto de lo que tiene la cuenta.
  const key = JSON.stringify(vehicles);
  useEffect(() => {
    if (!name || !ready.current) return;
    const t = setTimeout(push, 800);
    return () => clearTimeout(t);
  }, [key, name]);

  return null;
}
