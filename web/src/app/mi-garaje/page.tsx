import type { Metadata } from "next";
import { connection } from "next/server";
import { GarageManager } from "@/components/GarageManager";
import { getVehicles } from "@/lib/api";

export const metadata: Metadata = {
  title: "Mi garaje",
  description: "Guarda tus vehículos y encuentra los repuestos que les sirven.",
  robots: { index: false, follow: true },
};

/** "Tu garaje" de Amazon, versión Districauchos. Los vehículos se guardan en el navegador (sin cuenta). */
export default async function GaragePage() {
  await connection();
  const options = await getVehicles()
    .then((brands) => brands.map((b) => ({ brand: b.brand, models: b.models.map((m) => m.name) })))
    .catch(() => []);
  return <GarageManager options={options} />;
}
