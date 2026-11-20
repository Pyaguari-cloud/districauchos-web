import type { Metadata } from "next";
import Image from "next/image";
import { allVehiclePhotos } from "@/lib/vehicle-photos";

export const metadata: Metadata = {
  title: "Créditos de fotos",
  description: "Autores y licencias de las fotos de vehículos usadas en Districauchos.",
  robots: { index: false, follow: true },
};

/** Las licencias CC BY / CC BY-SA piden nombrar al autor, enlazar la licencia y la fuente. */
export default function CreditsPage() {
  const photos = allVehiclePhotos();
  return (
    <div className="flex flex-col gap-5">
      <div>
        <h1 className="font-display text-3xl font-bold uppercase tracking-wide">Créditos de fotos</h1>
        <p className="mt-1 max-w-3xl text-muted">
          Las fotos de vehículos de &quot;Mi garaje&quot; vienen de Wikimedia Commons y se usan según sus licencias libres.
          Gracias a sus autores. Las fotos se recortaron y redujeron de tamaño.
        </p>
      </div>
      <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {photos.map(({ photo, vehicles }) => (
          <li key={photo.src} className="flex gap-3 rounded-xl border border-line bg-white p-3">
            <Image src={photo.src} alt={vehicles.join(", ")} width={96} height={60} sizes="96px" className="h-15 w-24 shrink-0 rounded-md object-cover" />
            <div className="min-w-0 text-sm">
              <p className="font-semibold">{vehicles.join(" · ")}</p>
              <p className="text-muted [overflow-wrap:anywhere]">Autor: {photo.author}</p>
              <p className="text-muted">
                {photo.licenseUrl ? (
                  <a href={photo.licenseUrl} target="_blank" rel="noopener noreferrer license" className="underline underline-offset-2">{photo.license}</a>
                ) : photo.license}
                {" · "}
                <a href={photo.source} target="_blank" rel="noopener noreferrer" className="underline underline-offset-2">Fuente</a>
              </p>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
