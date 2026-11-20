"use client";

import { dayText, estimate, minutesToCutoff } from "@/lib/eta";
import { useNow } from "@/lib/use-now";

const countdown = (min: number) => {
  const h = Math.floor(min / 60), m = min % 60;
  return h > 0 ? `${h} h ${m} min` : `${m} min`;
};

/**
 * "Recíbelo hoy en Quito si pides en las próximas 2 h 15 min" (como Amazon). Solo para repuestos con stock:
 * los que no tienen se venden bajo pedido y su tiempo se muestra en el carrito.
 * Se dibuja solo en el navegador (depende de la hora actual de Ecuador).
 */
export function DeliveryPromise({ available }: { available: boolean }) {
  const now = useNow();
  if (!available || !now) return null;

  const quito = estimate("quito", now);
  const national = estimate("national", now);
  const left = minutesToCutoff(now);
  const sameMonth = national.from.getUTCMonth() === national.to.getUTCMonth();

  return (
    <div className="rounded-xl border border-line bg-white p-4 text-sm">
      <p>
        <span className="font-semibold">Envío gratis en Quito:</span> recíbelo{" "}
        <strong className="text-ok">{dayText(quito.from, quito.today)}</strong>
        {left !== null && left > 0 && <> si pides en las próximas <strong>{countdown(left)}</strong></>}.
      </p>
      <p className="mt-1 text-muted">
        A provincia (+$5): entre {dayText(national.from, national.today, !sameMonth)} y {dayText(national.to, national.today)}.
        También retiro en local.
      </p>
    </div>
  );
}
