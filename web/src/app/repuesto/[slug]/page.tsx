import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { cache } from "react";
import { Availability } from "@/components/Availability";
import { PartImage } from "@/components/PartImage";
import { WhatsAppButton } from "@/components/WhatsAppButton";
import { getItem, type Vehicle } from "@/lib/api";
import { displayTitle, money, priceRange, sentenceCase, vehicleLabel } from "@/lib/format";
import { quoteOneLink } from "@/lib/whatsapp";

type Props = { params: Promise<{ slug: string }> };

// Una sola llamada a la API por request aunque la usen metadata y página.
const load = cache(getItem);

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const item = await load((await params).slug);
  if (!item) return { title: "Repuesto no encontrado", robots: { index: false } };
  const title = displayTitle(item.title);
  const vehicles = uniqueVehicles(item.vehicles).slice(0, 4).join(", ");
  const description = `${title}. OEM ${item.oem}${vehicles ? `. Compatible con ${vehicles}` : ""}. ${
    item.available ? "Disponible" : "Consulta disponibilidad"
  } en Districauchos, Quito. Cotiza por WhatsApp.`;
  return {
    title: `${title} (OEM ${item.oem})`,
    description,
    alternates: { canonical: `/repuesto/${item.slug}` },
    openGraph: { title, description, url: `/repuesto/${item.slug}` },
  };
}

export default async function ItemPage({ params }: Props) {
  const item = await load((await params).slug);
  if (!item) notFound();

  const title = displayTitle(item.title);
  const vehicles = groupVehicles(item.vehicles);
  const wa = quoteOneLink({ oem: item.oem, title, price: item.pvpMin });
  const otherNames = item.description
    .split(" | ")
    .map(displayTitle)
    .filter((d) => d.toLowerCase() !== title.toLowerCase());

  // JSON-LD Product para Google. Sin precio (decisión pendiente con el cliente, brief §12.5).
  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "Product",
    name: title,
    mpn: item.oem,
    sku: item.oem,
    category: item.category ?? undefined,
    brand: { "@type": "Brand", name: "Districauchos" },
    ...(item.image ? { image: item.image } : {}),
  };

  return (
    <article className="flex flex-col gap-6">
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, "\\u003c") }} />

      <nav aria-label="Ruta" className="text-sm text-muted">
        <ol className="flex flex-wrap gap-1">
          <li><Link href="/" className="underline underline-offset-2">Inicio</Link> /</li>
          {item.category && (
            <li>
              <Link href={`/buscar?categoria=${encodeURIComponent(item.category)}`} className="underline underline-offset-2">
                {sentenceCase(item.category)}
              </Link>{" "}
              /
            </li>
          )}
          <li aria-current="page" className="line-clamp-1">OEM {item.oem}</li>
        </ol>
      </nav>

      <div className="grid gap-6 md:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <PartImage image={item.image} category={item.category} title={title} large />

        <div className="flex flex-col gap-4">
          <div>
            <h1 className="font-display text-3xl font-bold leading-tight">{title}</h1>
            <p className="mt-1 text-muted">
              Código OEM <span className="font-mono font-semibold text-brand-ink">{item.oem}</span>
              {item.oemAliases.length > 0 && <> · también {item.oemAliases.join(", ")}</>}
            </p>
          </div>

          <Availability available={item.available} />

          <div className="rounded-xl border border-line bg-white p-4">
            <p className="text-sm font-semibold uppercase tracking-wide text-muted">Precio público</p>
            <p className="font-display text-4xl font-bold">{priceRange(item.pvpMin, item.pvpMax)}</p>
            {item.mayorMin !== null && (
              <p className="mt-2 flex flex-wrap items-center gap-2">
                <span className="rounded bg-brand-red px-2 py-0.5 text-sm font-bold uppercase text-white">Por mayor</span>
                <span className="font-display text-2xl font-bold">desde {money(item.mayorMin)}</span>
                <span className="text-sm text-muted">en compras desde $200</span>
              </p>
            )}
          </div>

          {wa && <WhatsAppButton href={wa} label="Cotizar por WhatsApp" full />}

          {vehicles.length > 0 && (
            <section aria-labelledby="compatibles">
              <h2 id="compatibles" className="mb-2 font-display text-xl font-bold uppercase tracking-wide">Vehículos compatibles</h2>
              <ul className="flex flex-col gap-1.5">
                {vehicles.map(({ brand, models }) => (
                  <li key={brand}>
                    <Link href={`/buscar?marca=${encodeURIComponent(brand)}`} className="font-semibold underline underline-offset-2">
                      {brand}
                    </Link>
                    {models.length > 0 && <span className="text-muted">: {models.join(", ")}</span>}
                  </li>
                ))}
              </ul>
            </section>
          )}
        </div>
      </div>

      {item.variants.length > 1 && (
        <section aria-labelledby="opciones" className="rounded-xl border border-line bg-white p-4">
          <h2 id="opciones" className="mb-1 font-display text-xl font-bold uppercase tracking-wide">
            {item.variants.length} opciones para este repuesto
          </h2>
          <p className="mb-3 text-sm text-muted">Mismo código OEM, distinto fabricante. Pregunta por la que prefieras al cotizar.</p>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[22rem] text-left">
              <thead className="text-sm text-muted">
                <tr>
                  <th scope="col" className="py-2 pr-3 font-semibold">Código</th>
                  <th scope="col" className="py-2 pr-3 font-semibold">Precio</th>
                  <th scope="col" className="py-2 font-semibold">Disponibilidad</th>
                </tr>
              </thead>
              <tbody>
                {item.variants.map((v) => (
                  <tr key={v.code} className="border-t border-line">
                    <td className="py-2 pr-3 font-mono text-sm">{v.code}</td>
                    <td className="py-2 pr-3">
                      {money(v.pvp)}
                      {v.precioMayor !== null && <span className="block text-sm text-brand-red">Por mayor {money(v.precioMayor)}</span>}
                    </td>
                    <td className="py-2"><Availability available={v.available} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {otherNames.length > 0 && (
        <section aria-labelledby="tambien">
          <h2 id="tambien" className="mb-2 font-display text-xl font-bold uppercase tracking-wide">También conocido como</h2>
          <ul className="list-disc pl-5 text-muted">
            {otherNames.map((n) => <li key={n}>{n}</li>)}
          </ul>
        </section>
      )}
    </article>
  );
}

function groupVehicles(vehicles: Vehicle[]) {
  const map = new Map<string, string[]>();
  for (const v of vehicles) {
    const list = map.get(v.brand) ?? [];
    if (v.model) list.push(v.model);
    map.set(v.brand, list);
  }
  return [...map].map(([brand, models]) => ({ brand, models }));
}

function uniqueVehicles(vehicles: Vehicle[]) {
  return [...new Set(vehicles.map((v) => vehicleLabel(v.brand, v.model)))];
}
