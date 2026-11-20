"use client";

import { useEffect, useState } from "react";

/**
 * Hora actual solo en el navegador (null en el servidor y en el primer render, para no romper la hidratación).
 * Se actualiza cada minuto: la fecha de entrega cambia al pasar las 17:00.
 */
export function useNow(): Date | null {
  const [now, setNow] = useState<Date | null>(null);
  useEffect(() => {
    const tick = () => setNow(new Date());
    tick();
    const id = setInterval(tick, 60_000);
    return () => clearInterval(id);
  }, []);
  return now;
}
