"use client";

import { googleOemHref } from "@/lib/google-ai";
import { useLastPlate } from "@/lib/last-plate";

/**
 * En los resultados de búsqueda con un vehículo: botón para buscar el OEM, las medidas y la foto del repuesto con la
 * IA de Google, sin volver a consultar la placa (pedido del cliente 2026-11-24). Si la última placa consultada es de
 * este mismo carro, la pregunta va con los datos exactos de la ANT y el chasis; si no, con el vehículo del catálogo.
 * "missing": no hubo coincidencia exacta en el inventario; se muestra más visible y con el texto de adaptar.
 */
export function GoogleOemBox({ query, brand, model, year, engine, label, missing }: {
  query: string; brand: string; model: string | null; year: number | null; engine: string | null; label: string;
  missing: boolean;
}) {
  const last = useLastPlate();
  const same = last && last.brand === brand && last.model === model && last.make ? last : null;
  const href = googleOemHref(same
    ? { vehicle: [same.make, same.modelName].filter(Boolean).join(" "), year: same.year, engine: same.engine, vin: same.vin, part: query }
    : { vehicle: label, year, engine, part: query });

  return (
    <div className={`flex flex-col gap-3 rounded-lg border px-4 py-3 sm:flex-row sm:items-center sm:justify-between ${missing ? "border-brand-yellow bg-ask-bg" : "border-line bg-white"}`}>
      <p className="max-w-2xl text-sm">
        {missing
          ? <><strong>¿No está exacto?</strong> Busca el código OEM y las medidas con la IA de Google: con las medidas
            vemos si otro repuesto nuestro le sirve o se puede adaptar. </>
          : <>¿Quieres confirmar el código? Búscalo con la IA de Google, con medidas y foto de cómo luce. </>}
        <span className="text-muted">
          {same ? `Usamos los datos de la placa ${same.plate}. ` : ""}Google te pedirá confirmar: toca “Sí, continuar”.
        </span>
      </p>
      <a href={href} target="_blank" rel="noopener noreferrer"
        className="flex min-h-11 shrink-0 items-center justify-center rounded-lg bg-brand-black px-4 text-center font-display text-sm font-bold uppercase tracking-wide text-white hover:bg-brand-ink">
        Buscar OEM y medidas con la IA de Google ↗
      </a>
    </div>
  );
}
