import Link from "next/link";
import { connection } from "next/server";
import { HowItWorks } from "@/components/HowItWorks";
import { ProductGrid } from "@/components/ProductCard";
import { StoreCard } from "@/components/StoreCard";
import { negocio } from "@/content/negocio";
import { SearchBox } from "@/components/SearchBox";
import { getCategories, getVehicles, searchCatalog } from "@/lib/api";
import { isRealCategory, sentenceCase } from "@/lib/format";
import { jsonLd, organizationJsonLd } from "@/lib/structured-data";

export default async function HomePage() {
  // Se renderiza al pedirla (no en el build): los datos vienen de la API y se cachean 5 min.
  await connection();
  const [topSellers, categories, brands] = await Promise.all([
    searchCatalog({ available: true, pageSize: 8 }), // sin texto: ordena clase A primero
    getCategories(),
    getVehicles(),
  ]);

  return (
    <div className="flex flex-col gap-10">
      <section className="-mx-4 -mt-6 bg-brand-ink px-4 py-10 text-white sm:rounded-b-2xl">
        <div className="mx-auto flex max-w-3xl flex-col gap-4">
          <h1 className="font-display text-3xl font-bold uppercase leading-tight tracking-wide sm:text-4xl">
            Encuentra el repuesto <span className="text-brand-yellow">para tu vehículo</span>
          </h1>
          <p className="text-gray-300">Busca por nombre, por vehículo o por código OEM. Cotiza al instante por WhatsApp.</p>
          <SearchBox size="lg" />
        </div>
      </section>

      <ul className="grid grid-cols-3 gap-2 text-center sm:gap-4" aria-label="Districauchos en números">
        {negocio.highlights.map((h) => (
          <li key={h.label} className="rounded-xl border border-line bg-white px-2 py-3">
            <p className="font-display text-2xl font-bold text-brand-red sm:text-3xl">{h.value}</p>
            <p className="text-xs text-muted sm:text-sm">{h.label}</p>
          </li>
        ))}
      </ul>

      <section aria-labelledby="marcas">
        <h2 id="marcas" className="mb-3 font-display text-2xl font-bold uppercase tracking-wide">Busca por marca</h2>
        <ul className="flex flex-wrap gap-2">
          {brands.slice(0, 14).map((b) => (
            <li key={b.brand}>
              <Link
                href={`/buscar?marca=${encodeURIComponent(b.brand)}`}
                className="inline-flex min-h-11 items-center gap-2 rounded-full border border-line bg-white px-4 font-semibold hover:border-gray-500"
              >
                {b.brand} <span className="text-sm font-normal text-muted">{b.count}</span>
              </Link>
            </li>
          ))}
        </ul>
      </section>

      <section aria-labelledby="categorias">
        <h2 id="categorias" className="mb-3 font-display text-2xl font-bold uppercase tracking-wide">Categorías</h2>
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {categories.filter((c) => isRealCategory(c.name)).slice(0, 12).map((c) => (
            <li key={c.name}>
              <Link
                href={`/buscar?categoria=${encodeURIComponent(c.name)}`}
                className="flex min-h-16 items-center justify-between gap-2 rounded-xl border border-line bg-white px-4 py-3 font-semibold hover:border-gray-500"
              >
                <span>{sentenceCase(c.name)}</span>
                <span className="text-sm font-normal text-muted">{c.count}</span>
              </Link>
            </li>
          ))}
        </ul>
      </section>

      <section aria-labelledby="mas-vendidos">
        <div className="mb-3 flex items-baseline justify-between gap-4">
          <h2 id="mas-vendidos" className="font-display text-2xl font-bold uppercase tracking-wide">Los más pedidos</h2>
          <Link href="/buscar" className="font-semibold underline underline-offset-4">Ver todo</Link>
        </div>
        <ProductGrid items={topSellers.items} />
      </section>

      <HowItWorks />

      <section aria-labelledby="locales">
        <div className="mb-3 flex items-baseline justify-between gap-4">
          <h2 id="locales" className="font-display text-2xl font-bold uppercase tracking-wide">Visítanos en {negocio.city}</h2>
          <Link href="/locales" className="font-semibold underline underline-offset-4">Horarios</Link>
        </div>
        <div className="grid gap-4 md:grid-cols-3">
          {negocio.stores.map((s) => <StoreCard key={s.id} store={s} compact />)}
        </div>
      </section>

      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd(organizationJsonLd())} />
    </div>
  );
}
