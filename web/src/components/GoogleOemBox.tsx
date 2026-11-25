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

  // Botón grande con halo de colores (pedido del cliente 2026-11-25: en los locales no encontraban el botón chico).
  // La ventana "¿Quieres continuar?" es de Google y no se puede quitar desde aquí; por eso el aviso de tocar
  // "Sí, continuar" va grande y junto al botón (algunos vendedores tocaban "No, gracias").
  return (
    <div className={`flex flex-col gap-4 rounded-lg border px-4 py-4 lg:flex-row lg:items-center lg:justify-between ${missing ? "border-brand-yellow bg-ask-bg" : "border-line bg-white"}`}>
      <p className="max-w-2xl text-sm">
        {missing
          ? <><strong>¿No está exacto?</strong> Busca el código OEM y las medidas con la IA de Google: con las medidas
            vemos si otro repuesto nuestro le sirve o se puede adaptar.</>
          : <>¿Quieres confirmar el código? Búscalo con la IA de Google, con medidas y foto de cómo luce.</>}
        {same ? <span className="text-muted"> Usamos los datos de la placa {same.plate}.</span> : null}
      </p>
      <div className="flex shrink-0 flex-col items-center gap-2">
        <span className="ai-glow inline-flex w-full max-w-md sm:w-auto">
          <a href={href} target="_blank" rel="noopener noreferrer"
            className="flex min-h-16 w-full items-center justify-center gap-3 rounded-full bg-brand-black px-7 py-3 text-left font-display text-lg font-bold uppercase leading-tight tracking-wide text-white hover:bg-brand-ink focus-visible:outline-offset-4 sm:text-xl">
            <svg aria-hidden="true" viewBox="0 0 24 24" className="size-8 shrink-0" fill="currentColor">
              <path d="M10 2l1.9 5.6L17.5 9.5l-5.6 1.9L10 17l-1.9-5.6L2.5 9.5l5.6-1.9z" />
              <path d="M18.5 13l.95 2.55L22 16.5l-2.55.95L18.5 20l-.95-2.55L15 16.5l2.55-.95z" />
            </svg>
            <span>Buscar OEM y medidas<br className="hidden sm:block" /> con la IA de Google</span>
          </a>
        </span>
        <p className="text-center text-sm font-semibold">
          Google preguntará si quieres continuar: toca <span className="whitespace-nowrap rounded bg-brand-yellow px-1.5 py-0.5">“Sí, continuar”</span>
        </p>
      </div>
    </div>
  );
}
