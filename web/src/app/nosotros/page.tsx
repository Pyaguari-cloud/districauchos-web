import type { Metadata } from "next";
import Link from "next/link";
import { negocio } from "@/content/negocio";

export const metadata: Metadata = {
  title: "Quiénes somos",
  description:
    "Más de 30 años en repuestos de caucho automotriz en Quito. Importadores directos desde Corea, Japón y Tailandia: mangueras, bujes, bandas y más.",
  alternates: { canonical: "/nosotros" },
};

/* Texto basado en la historia que envió el cliente (2026-11-18), corregido y ordenado. */
export default function AboutPage() {
  const origins = negocio.importOrigins.join(", ").replace(/, ([^,]*)$/, " y $1");
  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <h1 className="font-display text-3xl font-bold uppercase tracking-wide">Quiénes somos</h1>

      <p className="text-lg">
        Somos <strong>Districauchos VCH</strong>, una empresa con <strong>más de 30 años de experiencia</strong> en la venta
        al por menor y al por mayor de repuestos de caucho automotriz.
      </p>

      <ul className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {negocio.highlights.map((h) => (
          <li key={h.label} className="rounded-xl border border-line bg-white p-4">
            <p className="font-display text-3xl font-bold text-brand-red">{h.value}</p>
            <p className="text-muted">{h.label}</p>
          </li>
        ))}
      </ul>

      <section className="flex flex-col gap-3">
        <h2 className="font-display text-2xl font-bold uppercase tracking-wide">Importadores directos</h2>
        <p>
          Somos importadores directos y distribuidores: traemos nuestros productos desde <strong>{origins}</strong> con
          nuestra línea <strong>Importadora VCH</strong>. Así cuidamos la calidad y la procedencia de cada repuesto y
          ofrecemos precios al por mayor.
        </p>
        <Link href="/buscar?importado=1" className="self-start font-semibold underline underline-offset-4">
          Ver productos de importación directa
        </Link>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="font-display text-2xl font-bold uppercase tracking-wide">Lo que encuentras con nosotros</h2>
        <ul className="list-disc space-y-1 pl-5">
          <li>Más de <strong>10.000 tipos de mangueras</strong>: de radiador, calefacción, hidráulicas y más.</li>
          <li>Más de <strong>8.000 tipos de bujes</strong> de suspensión.</li>
          <li>Bandas automotrices, cauchos de barra estabilizadora, bases de motor y muchos otros repuestos.</li>
        </ul>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="font-display text-2xl font-bold uppercase tracking-wide">Te atendemos donde estés</h2>
        <p>
          Atendemos a talleres, mecánicos, almacenes y conductores en nuestros {negocio.stores.length} locales de {negocio.city} (
          {negocio.stores.map((s) => s.name).join(", ")}). Hacemos <strong>envíos a todo el Ecuador</strong> y también
          envíos internacionales.
        </p>
      </section>

      <div className="flex flex-wrap gap-3">
        <Link href="/buscar" className="inline-flex min-h-12 items-center rounded-lg bg-brand-yellow px-5 font-display text-lg font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark">
          Ver repuestos
        </Link>
        <Link href="/locales" className="inline-flex min-h-12 items-center rounded-lg border-2 border-brand-black px-5 font-display text-lg font-bold uppercase tracking-wide hover:bg-brand-yellow">
          Nuestros locales
        </Link>
      </div>
    </div>
  );
}
