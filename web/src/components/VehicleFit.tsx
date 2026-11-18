"use client";

import type { Vehicle } from "@/lib/api";
import { useMyVehicle, vehicleText } from "@/lib/vehicle";

/** ¿Le sirve a "Mi vehículo"? Según los vehículos detectados en la descripción. */
export function VehicleFit({ vehicles }: { vehicles: Vehicle[] }) {
  const mine = useMyVehicle();
  if (!mine) return null;

  const sameBrand = vehicles.filter((v) => v.brand === mine.brand);
  const fits = mine.model
    ? sameBrand.some((v) => v.model === mine.model || v.model === null) // null = toda la marca
    : sameBrand.length > 0;

  return fits ? (
    <p className="w-fit rounded-lg bg-ok-bg px-3 py-2 font-semibold text-ok">✓ Compatible con tu {vehicleText(mine)}</p>
  ) : (
    <p className="w-fit rounded-lg bg-ask-bg px-3 py-2 text-ask">
      No figura como compatible con tu {vehicleText(mine)}. Pregúntanos por WhatsApp para confirmar.
    </p>
  );
}
