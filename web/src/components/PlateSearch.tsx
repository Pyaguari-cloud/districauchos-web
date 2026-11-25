"use client";

import { useRouter } from "next/navigation";
import { useEffect, useId, useRef, useState } from "react";
import { CloseIcon } from "./icons";
import { googleOemHref } from "@/lib/google-ai";
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
 * abre el Modo IA de Google con la pregunta ya escrita (OEM, medidas y foto), gratis. La IA sugiere; el vendedor confirma.
 */
/**
 * Versión grande del inicio (cliente 2026-11-24: "no encontraban el botón"; la chica de la barra se quitó para no
 * repetir). La entrada se ve como una placa de Ecuador y no toma el foco sola (no salta el teclado al abrir la página).
 */
export function PlateSearch({ options }: { options: VehicleOption[] }) {
  const router = useRouter();
  const inputId = useId();
  const input = useRef<HTMLInputElement>(null);
  const [plate, setPlate] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState<PlateResult | null>(null);
  const [part, setPart] = useState("");
  // La ficha va en una ventana emergente (pedido 2026-11-24): así la portada no crece ni se descuadra con el carrusel.
  const dialog = useRef<HTMLDialogElement>(null);
  const [open, setOpen] = useState(false);
  useEffect(() => {
    const d = dialog.current;
    if (!d) return;
    if (open && !d.open) d.showModal();
    if (!open && d.open) d.close();
  }, [open, result]);

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
        setPart("");
        setOpen(true);
        // Para el pedido: placa y chasis del carro que se buscó (solo datos del vehículo).
        if (r.found && r.brand) saveLastPlate({ plate: r.plate, vin: r.vin, brand: r.brand, model: r.catalogModel, make: r.make, modelName: r.model, year: r.year, engine: r.engine });
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
    router.push(vehicleSearchHref({ brand: option.brand, model, year, engine }, wanted ? { q: wanted } : {}));
  }

  // Modo IA de Google con la pregunta ya escrita: los datos tal como los da la ANT, que son los más exactos.
  const googleHref = result?.found && wanted
    ? googleOemHref({ vehicle: [result.make, result.model].filter(Boolean).join(" "), year: result.year, engine: result.engine, vin: result.vin, part: wanted })
    : null;

  const row = "flex justify-between gap-3 border-b border-white/10 py-1.5 last:border-0";
  const dt = "text-white/60";
  const dd = "text-right font-semibold text-white";

  return (
    <div>
      <form onSubmit={lookup} className="flex flex-col gap-3">
        <label htmlFor={inputId} className="sr-only">Placa de tu vehículo</label>
        {/* Se ve como una placa de Ecuador: franja "ECUADOR" arriba y letras grandes. */}
        <div className="mx-auto w-full max-w-sm overflow-hidden rounded-xl border-4 border-gray-300 bg-white shadow-inner focus-within:border-brand-yellow">
          <p aria-hidden="true" className="bg-gray-200 py-0.5 text-center font-display text-xs font-bold tracking-[0.35em] text-gray-600">ECUADOR</p>
          <input
            ref={input} id={inputId} value={plate} maxLength={9} autoComplete="off" autoCapitalize="characters" spellCheck={false}
            inputMode="text" enterKeyHint="search" placeholder="ABC-1234" aria-describedby={`${inputId}-ayuda`}
            onChange={(e) => { setPlate(e.target.value.toUpperCase()); setError(""); }}
            className="h-20 w-full bg-transparent text-center font-display text-5xl font-bold tracking-[0.12em] text-brand-ink placeholder:text-gray-300 focus:outline-none sm:h-24 sm:text-6xl"
          />
        </div>
        <button type="submit" disabled={busy}
          className="flex min-h-14 w-full items-center justify-center gap-2 rounded-lg bg-brand-yellow px-5 font-display text-xl font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark disabled:opacity-50">
          {busy ? "Consultando…" : "Buscar por placa"}
        </button>
      </form>
      <p id={`${inputId}-ayuda`} className="mt-1.5 text-center text-xs text-white/50">
        Solo usamos la placa para saber marca, modelo y año. No pedimos ni guardamos datos del dueño.
      </p>

      {error && <p role="alert" className="mt-2 text-sm font-semibold text-red-300">{error}</p>}

      {result && !open && (
        <p className="mt-2 text-center text-sm text-white/80">
          Placa {result.plate}:{" "}
          <button type="button" onClick={() => setOpen(true)} className="font-semibold text-brand-yellow underline underline-offset-2">
            Ver de nuevo
          </button>
        </p>
      )}

      <dialog ref={dialog} aria-labelledby={`${inputId}-titulo`} onClose={() => setOpen(false)}
        onClick={(e) => { if (e.target === e.currentTarget) setOpen(false); }}
        className="m-0 mt-auto max-h-[90dvh] w-full max-w-none overflow-y-auto rounded-t-2xl border-0 bg-brand-ink p-0 text-white [color-scheme:dark] backdrop:bg-black/70 sm:m-auto sm:max-w-md sm:rounded-2xl">
        {result && (
          <div className="p-5 pb-[max(1.25rem,env(safe-area-inset-bottom))] sm:p-6">
            <div className="mb-3 flex items-start justify-between gap-3">
              <h2 id={`${inputId}-titulo`} className="font-display text-2xl font-bold uppercase tracking-wide">
                {result.found ? <>Tu <span className="text-brand-yellow">vehículo</span></> : "Placa no encontrada"}
              </h2>
              <button type="button" onClick={() => setOpen(false)}
                className="-m-2 flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-white/80 hover:bg-white/10 hover:text-white">
                <CloseIcon /><span className="sr-only">Cerrar</span>
              </button>
            </div>
        {result && !result.found && (
          <p className="text-white">
            No encontramos la placa <strong>{result.plate}</strong>. Revisa que esté bien escrita, o elige tu vehículo en la
            lista de marca y modelo.
          </p>
        )}

        {result?.found && (
          <div>
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
                La IA de Google sugiere el código, las medidas y cómo luce; confírmalo con nosotros antes de comprar. Google te pedirá confirmar la búsqueda: toca “Sí, continuar”. Si muestra solo enlaces, escríbele “dame la tabla”.
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
        )}
      </dialog>
    </div>
  );
}
