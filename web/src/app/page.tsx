import Link from "next/link";
import { connection } from "next/server";
import { HowItWorks } from "@/components/HowItWorks";
import { PartImage } from "@/components/PartImage";
import { brandLogo, categoryExamples } from "@/content/marcas";
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
  // Pedido del cliente: primero lo que importan directamente (Importadora VCH), luego el resto.
  const [imported, topSellers, categories, brands] = await Promise.all([
    searchCatalog({ available: true, vch: true, pageSize: 8 }),
    searchCatalog({ available: true, vch: false, pageSize: 8 }), // sin texto: clase A primero
    getCategories(),
    getVehicles(),
  ]);

  return (
    <div className="flex flex-col gap-10">
      <section className="-mx-4 -mt-6 bg-brand-ink px-4 py-8 text-white sm:mx-0 sm:mt-0 sm:rounded-2xl sm:py-12">
        <div className="mx-auto flex max-w-3xl flex-col gap-4">
          <h1 className="font-display text-3xl font-bold uppercase leading-tight tracking-wide sm:text-4xl">
            Encuentra el repuesto <span className="text-brand-yellow">para tu vehículo</span>
          </h1>
          <p className="text-gray-300">Busca por nombre, por vehículo o por código OEM. Cotiza al instante por WhatsApp.</p>
          {/* En celular ya está el buscador del encabezado justo arriba: no se repite. */}
          <div className="hidden sm:block"><SearchBox size="lg" /></div>
        </div>
      </section>

      <ul className="grid grid-cols-2 gap-2 text-center sm:grid-cols-4 sm:gap-4" aria-label="Districauchos en números">
        {negocio.highlights.map((h) => (
          <li key={h.label} className="rounded-xl border border-line bg-white px-2 py-3">
            <p className="font-display text-2xl font-bold text-brand-red sm:text-3xl">{h.value}</p>
            <p className="text-xs text-muted sm:text-sm">{h.label}</p>
          </li>
        ))}
      </ul>

      <section aria-labelledby="importacion" className="rounded-2xl border-2 border-brand-red/20 bg-white p-4 sm:p-6">
        <div className="mb-1 flex items-baseline justify-between gap-4">
          <h2 id="importacion" className="font-display text-2xl font-bold uppercase tracking-wide">
            <span className="text-brand-red">Importación directa</span> VCH
          </h2>
          <Link href="/buscar?importado=1" className="shrink-0 font-semibold underline underline-offset-4">Ver todos</Link>
        </div>
        <p className="mb-4 text-muted">Importamos directamente desde Corea, Japón y Tailandia. Precio al por mayor desde $200.</p>
        <ProductGrid items={imported.items} />
      </section>

      <section aria-labelledby="marcas">
        <h2 id="marcas" className="mb-3 font-display text-2xl font-bold uppercase tracking-wide">Busca por marca</h2>
        <ul className="grid grid-cols-3 gap-2 sm:grid-cols-4 sm:gap-3 lg:grid-cols-6">
          {brands.slice(0, 18).map((b) => {
            const logo = brandLogo(b.brand);
            return (
              <li key={b.brand}>
                <Link
                  href={`/buscar?marca=${encodeURIComponent(b.brand)}`}
                  className="flex h-full flex-col items-center justify-center gap-2 rounded-xl border border-line bg-white px-2 py-3 text-center transition hover:border-gray-500 hover:shadow-sm sm:py-4"
                >
                  <span className="flex h-12 w-full items-center justify-center sm:h-14">
                    {logo ? (
                      // eslint-disable-next-line @next/next/no-img-element -- SVG pequeño y estático
                      <img src={logo} alt="" loading="lazy" className="max-h-full max-w-[80%] object-contain" />
                    ) : (
                      <span className="font-display text-lg font-bold uppercase leading-tight tracking-wide text-gray-700">{b.brand}</span>
                    )}
                  </span>
                  <span className="text-sm font-semibold leading-tight">
                    {b.brand} <span className="block text-xs font-normal text-muted">{b.count} repuestos</span>
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
      </section>

      <section aria-labelledby="categorias">
        <h2 id="categorias" className="mb-3 font-display text-2xl font-bold uppercase tracking-wide">Categorías</h2>
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {categories.filter((c) => isRealCategory(c.name)).slice(0, 12).map((c) => {
            const examples = categoryExamples(c.name);
            return (
              <li key={c.name}>
                <Link
                  href={`/buscar?categoria=${encodeURIComponent(c.name)}`}
                  className="group flex h-full flex-col overflow-hidden rounded-xl border border-line bg-white transition hover:border-gray-500 hover:shadow-sm"
                >
                  <span className="block p-2 pb-0"><PartImage image={c.image ?? null} category={c.name} title={sentenceCase(c.name)} wide /></span>
                  <span className="flex flex-1 flex-col gap-1 p-3">
                    <span className="font-display text-lg font-bold uppercase leading-tight tracking-wide">{sentenceCase(c.name)}</span>
                    {examples && <span className="text-sm text-muted">{examples}</span>}
                    <span className="mt-auto pt-1 text-sm font-semibold text-brand-red">
                      Ver {c.count} productos <span aria-hidden="true" className="inline-block transition group-hover:translate-x-0.5">→</span>
                    </span>
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
      </section>

      <section aria-labelledby="mas-vendidos">
        <div className="mb-3 flex items-baseline justify-between gap-4">
          <h2 id="mas-vendidos" className="font-display text-2xl font-bold uppercase tracking-wide">Más repuestos</h2>
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
