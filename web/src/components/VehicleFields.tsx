"use client";

import { useMyVehicle } from "@/lib/vehicle";

/** Campos ocultos del buscador: cada búsqueda nueva se filtra por "Mi vehículo". */
export function VehicleFields() {
  const v = useMyVehicle();
  if (!v) return null;
  return (
    <>
      <input type="hidden" name="marca" value={v.brand} />
      {v.model && <input type="hidden" name="modelo" value={v.model} />}
      {v.model && v.year && <input type="hidden" name="anio" value={v.year} />}
    </>
  );
}
