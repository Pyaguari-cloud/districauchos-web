"use client";

import Link from "next/link";
import type { Vehicle } from "@/lib/api";
import { fitFor, rowText, sameModel, yearRanges, type CompatRow } from "@/lib/compat-match";
import { useMyVehicle, vehicleText } from "@/lib/vehicle";

/**
 * ¿Le sirve a "Mi vehículo"? Si hay compatibilidades por OEM (modelo, motor, años) se usa el año;
 * si no, los vehículos detectados en la descripción del inventario (solo marca y modelo).
 */
export function VehicleFit({ vehicles, compat = [] }: { vehicles: Vehicle[]; compat?: CompatRow[] }) {
  const mine = useMyVehicle();
  if (!mine) return null;
  const name = vehicleText(mine);

  const fit = fitFor(compat, mine);
  if (fit.kind === "yes") {
    return (
      <div className="w-fit rounded-lg bg-ok-bg px-3 py-2 text-ok">
        <p className="font-semibold">✓ Le sirve a tu {name}</p>
        <p className="text-sm">{fit.rows.map(rowText).join(" · ")}</p>
        {!fit.verified && <p className="text-sm opacity-80">Dato por confirmar: te lo confirmamos al cotizar.</p>}
      </div>
    );
  }
  if (fit.kind === "otherYears") {
    return (
      <div className="w-fit rounded-lg bg-ask-bg px-3 py-2 text-ask">
        <p className="font-semibold">Es para otros años de tu {mine.model}</p>
        <p className="text-sm">Sirve en {mine.model} {yearRanges(fit.rows)} (detalle abajo).</p>
        <p className="text-sm">Pregúntanos por WhatsApp y te ayudamos a encontrar el de tu {name}.</p>
      </div>
    );
  }
  if (fit.kind === "noYear") {
    return (
      <div className="w-fit rounded-lg bg-ok-bg px-3 py-2 text-ok">
        <p className="font-semibold">✓ Compatible con tu {name}</p>
        {mine.year ? (
          <p className="text-sm">Confirma el año con nosotros al cotizar.</p>
        ) : (
          <p className="text-sm">
            <Link href="/mi-garaje" className="underline underline-offset-2">Agrega el año en Mi garaje</Link> para confirmar si le sirve.
          </p>
        )}
      </div>
    );
  }

  const sameBrand = vehicles.filter((v) => v.brand === mine.brand);
  const fits = mine.model
    ? sameBrand.some((v) => v.model === mine.model || v.model === null) // null = toda la marca
    : sameBrand.length > 0;

  return fits ? (
    <p className="w-fit rounded-lg bg-ok-bg px-3 py-2 font-semibold text-ok">✓ Compatible con tu {name}</p>
  ) : (
    <p className="w-fit rounded-lg bg-ask-bg px-3 py-2 text-ask">
      No figura como compatible con tu {name}. Pregúntanos por WhatsApp para confirmar.
    </p>
  );
}

/**
 * Insignia de las tarjetas para el vehículo activo (Mi garaje / placa). Con datos por año dice si sirve para ese año;
 * sin ellos, "Compatible con tu …" si el inventario lo registra para esa marca y modelo (pedido 2026-11-24: que vuelva
 * el visto en cada tarjeta). Si no figura, no se dice nada en la tarjeta (el detalle lo explica en la ficha).
 */
export function CardFit({ compat, brands = [], models = [] }: { compat?: CompatRow[]; brands?: string[]; models?: string[] }) {
  const mine = useMyVehicle();
  if (!mine) return null;
  const fit = fitFor(compat ?? [], mine);
  const ok = "w-fit rounded bg-ok-bg px-1.5 py-0.5 text-sm font-semibold text-ok";
  if (fit.kind === "yes") return <p className={ok}>✓ Sirve para tu {mine.model} {mine.year}</p>;
  if (fit.kind === "otherYears") {
    return <p className="w-fit rounded bg-ask-bg px-1.5 py-0.5 text-sm font-semibold text-ask">Es para {mine.model} {yearRanges(fit.rows)}</p>;
  }
  const brand = brands.some((b) => b.toLowerCase() === mine.brand.toLowerCase());
  const model = !mine.model || models.some((m) => sameModel(m, mine.model!));
  if (fit.kind === "noYear" || (brand && model)) {
    return <p className={ok}>✓ Compatible con tu {mine.model ?? mine.brand}</p>;
  }
  return null;
}
