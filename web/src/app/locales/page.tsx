import type { Metadata } from "next";
import { StoreCard } from "@/components/StoreCard";
import { StoresMap } from "@/components/StoresMap";
import { negocio } from "@/content/negocio";
import { jsonLd, storesJsonLd } from "@/lib/structured-data";

export const metadata: Metadata = {
  title: "Locales y horarios en Quito",
  description: "Visítanos en Tabiazo, Cusubamba o Maldonado. Direcciones, horarios, teléfonos y WhatsApp de Districauchos en Quito.",
  alternates: { canonical: "/locales" },
};

export default function StoresPage() {
  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="font-display text-3xl font-bold uppercase tracking-wide">Nuestros locales</h1>
        <p className="text-muted">{negocio.stores.length} locales en {negocio.city}. Escríbenos o visítanos.</p>
      </div>
      <StoresMap />
      <div className="grid gap-4 md:grid-cols-3">
        {negocio.stores.map((s) => <StoreCard key={s.id} store={s} />)}
      </div>
      <p className="text-muted">
        También puedes escribirnos a{" "}
        <a href={`mailto:${negocio.email}`} className="font-semibold text-brand-ink underline underline-offset-2">{negocio.email}</a>.
      </p>
      {storesJsonLd().map((d) => (
        <script key={d["@id"]} type="application/ld+json" dangerouslySetInnerHTML={jsonLd(d)} />
      ))}
    </div>
  );
}
