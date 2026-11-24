"use client";

import { useRouter } from "next/navigation";
import { useEffect, useId, useRef, useState } from "react";
import { saveLastPlate } from "@/lib/last-plate";
import { garage, vehicleSearchHref, YEARS } from "@/lib/vehicle";
import type { VehicleOption } from "./VehicleBar";

/** Lo que responde /api/placa: datos del vehículo (nunca del dueño) y cómo se llama en nuestro catálogo. */
type PlateResult = {
  plate: string; found: boolean; make: string | null; model: string | null; year: number | null;
  vehicleClass: string | null; vin: string | null; brand: string | null; catalogModel: string | null; engine: string | null;
};

/**
 * "Buscar por placa" (pedido del cliente 2026-11-23, como Mansuera): se escribe la placa, sale la ficha del
 * vehículo (marca, modelo, año, cilindraje, chasis) y "Buscar repuestos para mi vehículo" lo guarda en Mi garaje
 * y abre sus repuestos. Si la marca o el modelo no están en el catálogo, se busca lo más cercano que sí está.
 * "¿Qué repuesto buscas?" (pedido del cliente 2026-11-24): busca ese repuesto en el inventario para el carro exacto, o
 * abre el Modo IA de Google con la pregunta ya escrita (OEM + despiece), gratis. La IA sugiere; el vendedor confirma.
 */
export function PlateSearch({ options, onClose }: { options: VehicleOption[]; onClose: () => void }) {
  const router = useRouter();
  const inputId = useId();
  const input = useRef<HTMLInputElement>(null);
  const [plate, setPlate] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState<PlateResult | null>(null);
  const [part, setPart] = useState("");

  useEffect(() => input.current?.focus(), []);

  async function lookup(e: React.FormEvent) {
    e.preventDefault();
    const p = plate.trim();
    if (!/^[A-Za-z]{2,3}[\s-]?\d{3,4}[A-Za-z]?$/.test(p)) {
      setError("Escribe la placa así: ABC-1234 (o AB-123C si es moto).");
      return;
    }
    setBusy(true); setError(""); setResult(null);
    try {
      const res = await fetch("/api/placa", {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ placa: p }),
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) setError(typeof data.error === "string" ? data.error : "No pudimos consultar la placa. Inténtalo de nuevo.");
      else {
        const r = data as PlateResult;
        setResult(r);
        // Para el pedido: placa y chasis del carro que se buscó (solo datos del vehículo).
        if (r.found && r.brand) saveLastPlate({ plate: r.plate, vin: r.vin, brand: r.brand, model: r.catalogModel });
      }
    } catch {
      setError("Sin conexión. Revisa tu internet e inténtalo de nuevo.");
    } finally {
      setBusy(false);
    }
  }

  // Lo que sí tenemos en el catálogo: marca, modelo (si está) y cilindraje (si está en la lista de ese modelo).
  const option = result?.brand ? options.find((o) => o.brand === result.brand) : undefined;
  const model = option && result?.catalogModel && option.models.includes(result.catalogModel) ? result.catalogModel : null;
  const engine = model && result?.engine && option?.engines?.[model]?.includes(result.engine) ? result.engine : null;
  const year = model && result?.year && YEARS.includes(result.year) ? result.year : null;

  const wanted = part.trim().slice(0, 80);

  function go() {
    if (!option) return;
    garage.addAndActivate(option.brand, model, year, engine);
    onClose();
    router.push(vehicleSearchHref({ brand: option.brand, model, year, engine }, wanted ? { q: wanted } : {}));
  }

  // Modo IA de Google (udm=50) con la pregunta ya escrita: los datos tal como los da la ANT, que son los más exactos.
  // Formato fijo (tabla + cómo diferenciar + despiece) para que el vendedor lea siempre lo mismo. Probado 2026-11-24.
  const googleHref = result?.found && wanted
    ? `https://www.google.com/search?${new URLSearchParams({
        udm: "50",
        q: [
          `Vehículo de Ecuador: ${[result.make, result.model].filter(Boolean).join(" ")}` +
            (result.year ? `, año ${result.year}` : "") + (result.engine ? `, motor ${result.engine}` : "") +
            (result.vin ? `, chasis ${result.vin}` : "") + `. Repuesto: ${wanted}.`,
          "Responde así:",
          "1) Una tabla con columnas: Código OEM | Descripción exacta | Cuándo aplica (motor, caja manual/automática, con/sin aire, posición) | Equivalentes alternativos (Gates, INR, otras marcas) | Fuente.",
          "2) Cómo diferenciar las opciones si hay más de una.",
          "3) El diagrama de despiece donde aparece la pieza.",
          "Si no estás seguro de un código, márcalo como \"por confirmar\"; no inventes.",
          // A veces el Modo IA devuelve solo "principales resultados web"; pedirlo explícito lo reduce (2026-11-24).
          "Responde aquí mismo con la información completa; no me des solo una lista de enlaces.",
        ].join("\n"),
      })}`
    : null;

  const row = "flex justify-between gap-3 border-b border-white/10 py-1.5 last:border-0";
  const dt = "text-white/60";
  const dd = "text-right font-semibold text-white";

  return (
    <div className="rounded-lg border border-white/15 bg-white/5 p-3">
      <form onSubmit={lookup} className="flex flex-wrap items-end gap-2">
        <div className="flex min-w-0 flex-1 flex-col gap-0.5 sm:flex-none">
          <label htmlFor={inputId} className="text-xs font-semibold text-white/70">Placa de tu vehículo</label>
          <input
            ref={input} id={inputId} value={plate} maxLength={9} autoComplete="off" autoCapitalize="characters" spellCheck={false}
            placeholder="ABC-1234" onChange={(e) => { setPlate(e.target.value.toUpperCase()); setError(""); }}
            className="h-11 w-full min-w-0 rounded-lg border border-white/20 bg-brand-ink px-3 font-display text-lg tracking-widest text-white placeholder:text-white/30 focus:border-brand-yellow focus:outline-none sm:w-56"
          />
        </div>
        <button type="submit" disabled={busy}
          className="h-11 rounded-lg bg-brand-yellow px-5 font-display font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark disabled:opacity-50">
          {busy ? "Consultando…" : "Consultar"}
        </button>
        <button type="button" onClick={onClose} className="h-11 px-2 text-sm text-white/80 underline underline-offset-2 hover:text-white">
          Cerrar
        </button>
      </form>
      <p className="mt-1.5 text-xs text-white/50">
        Solo usamos la placa para saber marca, modelo y año. No pedimos ni guardamos datos del dueño.
      </p>

      {error && <p role="alert" className="mt-2 text-sm font-semibold text-red-300">{error}</p>}

      {result && !result.found && (
        <p role="status" className="mt-3 text-sm text-white">
          No encontramos la placa <strong>{result.plate}</strong>. Revisa que esté bien escrita, o elige tu vehículo en la
          lista de marca y modelo.
        </p>
      )}

      {result?.found && (
        <div role="status" className="mt-3 max-w-md">
          <p className="font-display text-sm font-bold uppercase tracking-wide text-brand-yellow">Vehículo encontrado</p>
          <dl className="mt-1 text-sm">
            <div className={row}><dt className={dt}>Placa</dt><dd className={dd}>{result.plate}</dd></div>
            {result.make && <div className={row}><dt className={dt}>Marca</dt><dd className={dd}>{result.make}</dd></div>}
            {result.model && <div className={row}><dt className={dt}>Modelo</dt><dd className={dd}>{result.model}</dd></div>}
            {result.year && <div className={row}><dt className={dt}>Año</dt><dd className={dd}>{result.year}</dd></div>}
            {result.engine && <div className={row}><dt className={dt}>Cilindraje</dt><dd className={dd}>{result.engine}</dd></div>}
            {result.vehicleClass && <div className={row}><dt className={dt}>Tipo</dt><dd className={dd}>{result.vehicleClass}</dd></div>}
            {result.vin && <div className={row}><dt className={dt}>Chasis</dt><dd className={`${dd} break-all font-mono text-xs`}>{result.vin}</dd></div>}
          </dl>
          <div className="mt-3 flex flex-col gap-0.5">
            <label htmlFor={`${inputId}-parte`} className="text-xs font-semibold text-white/70">¿Qué repuesto buscas? (opcional)</label>
            <input
              id={`${inputId}-parte`} value={part} maxLength={80} autoComplete="off" placeholder="Ej.: manguera de calefacción"
              onChange={(e) => setPart(e.target.value)}
              onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); go(); } }}
              className="h-11 w-full min-w-0 rounded-lg border border-white/20 bg-brand-ink px-3 text-base text-white placeholder:text-white/30 focus:border-brand-yellow focus:outline-none"
            />
          </div>
          {option ? (
            <>
              {!model && (
                <p className="mt-2 text-xs text-white/70">
                  Ese modelo todavía no está en nuestra lista: te mostramos todos los repuestos {option.brand}.
                </p>
              )}
              <button type="button" onClick={go}
                className="mt-3 h-11 w-full rounded-lg bg-brand-yellow px-5 font-display font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark">
                {wanted ? "Buscar en nuestro inventario" : "Buscar repuestos para mi vehículo"}
              </button>
            </>
          ) : null}
          {googleHref ? (
            <a href={googleHref} target="_blank" rel="noopener noreferrer"
              className="mt-2 flex h-11 w-full items-center justify-center rounded-lg border border-white/30 px-4 text-center font-display text-sm font-bold uppercase tracking-wide text-white hover:border-brand-yellow hover:text-brand-yellow">
              Buscar el OEM con la IA de Google ↗
            </a>
          ) : (
            <p className="mt-2 text-xs text-white/60">Escribe el repuesto para buscar también su código OEM con la IA de Google.</p>
          )}
          {googleHref && (
            <p className="mt-1 text-xs text-white/50">
              La IA de Google sugiere el código y el despiece; confírmalo con nosotros antes de comprar. Si Google muestra solo enlaces, vuelve a tocar el botón o escríbele “dame la tabla”.
            </p>
          )}
          {!option && (
            <p className="mt-2 text-sm text-white">
              Todavía no tenemos repuestos registrados para {result.make ?? "esa marca"}. Escríbenos por WhatsApp y te
              ayudamos a conseguirlo.
            </p>
          )}
        </div>
      )}
    </div>
  );
}
