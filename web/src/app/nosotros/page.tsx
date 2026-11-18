import type { Metadata } from "next";
import Link from "next/link";
import { negocio } from "@/content/negocio";

export const metadata: Metadata = {
  title: "Quiénes somos",
  description: "Districauchos: repuestos de caucho automotriz en Quito. Mangueras, bujes, bandas, bases de motor y más.",
  alternates: { canonical: "/nosotros" },
};

/*
 * BORRADOR para aprobar con el cliente: solo usa hechos de su página actual
 * (eslogan, 3 locales, especialidad). Faltan años de experiencia e historia.
 */
export default function AboutPage() {
  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <h1 className="font-display text-3xl font-bold uppercase tracking-wide">Quiénes somos</h1>
      <p className="text-lg">
        En <strong>{negocio.name}</strong> nos especializamos en repuestos de caucho automotriz: mangueras, bujes, bandas,
        bases de motor y de amortiguador, guardapolvos y mucho más, para las marcas de vehículos más comunes del país.
      </p>
      <ul className="grid gap-3 sm:grid-cols-3">
        {negocio.highlights.map((h) => (
          <li key={h.label} className="rounded-xl border border-line bg-white p-4">
            <p className="font-display text-3xl font-bold text-brand-red">{h.value}</p>
            <p className="text-muted">{h.label}</p>
          </li>
        ))}
      </ul>
      <p>
        Atendemos a talleres, mecánicos, almacenes y conductores en nuestros {negocio.stores.length} locales de {negocio.city}:{" "}
        {negocio.stores.map((s) => s.name).join(", ")}. En esta página puedes buscar por tu vehículo o por código OEM,
        ver precios y disponibilidad, y cotizar al instante por WhatsApp.
      </p>
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
