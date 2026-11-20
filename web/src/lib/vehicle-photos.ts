import photos from "@/content/vehicle-photos.json";

/**
 * Foto de cada modelo de "Mi garaje" (pedido del cliente: "como Amazon").
 * Solo fotos con licencia libre de Wikimedia Commons (CC BY / CC BY-SA / CC0 / dominio público):
 * piden crédito al autor, que se muestra bajo la foto grande y en /creditos.
 * Las genera el script de fotos (ver PROGRESO.md) en public/vehiculos/ + content/vehicle-photos.json.
 */
export type VehiclePhoto = { src: string; author: string; license: string; licenseUrl: string | null; source: string };

const byKey = photos as Record<string, VehiclePhoto>;

export function vehiclePhoto(brand: string, model: string | null | undefined): VehiclePhoto | null {
  return model ? byKey[`${brand}|${model}`] ?? null : null;
}

/** Para la página de créditos: [marca|modelo, foto], agrupando los modelos que comparten foto. */
export function allVehiclePhotos() {
  const bySrc = new Map<string, { photo: VehiclePhoto; vehicles: string[] }>();
  for (const [key, photo] of Object.entries(byKey)) {
    const entry = bySrc.get(photo.src) ?? { photo, vehicles: [] };
    entry.vehicles.push(key.replace("|", " "));
    bySrc.set(photo.src, entry);
  }
  return [...bySrc.values()].sort((a, b) => a.vehicles[0].localeCompare(b.vehicles[0], "es"));
}

export const photoCredit = (p: VehiclePhoto) => `Foto: ${p.author} · ${p.license}`;
