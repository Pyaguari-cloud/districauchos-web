import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { ProductGrid } from "@/components/ProductCard";
import { WhatsAppButton } from "@/components/WhatsAppButton";
import { searchCatalog } from "@/lib/api";
import { rowsFromRanges } from "@/lib/compat-match";
import { sentenceCase, vehicleLabel } from "@/lib/format";
import { askLink } from "@/lib/whatsapp";
import { photoCredit, vehiclePhoto } from "@/lib/vehicle-photos";

type Params = { q?: string; marca?: string; modelo?: string; anio?: string; motor?: string; categoria?: string; disponible?: string; importado?: string; pagina?: string };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v)?.trim() || undefined;
const engineParam = (v: string | undefined) => (v && /^\d\.\d$/.test(v) ? v : undefined);
const yearParam = (v: string | undefined) => (v && /^\d{4}$/.test(v) && +v >= 1960 && +v <= 2100 ? v : undefined);

export async function generateMetadata({ searchParams }: { searchParams: Promise<Params> }): Promise<Metadata> {
  const p = await searchParams;
  const what = [p.q, p.importado === "1" && "Importación directa", p.marca && vehicleLabel(p.marca, p.modelo ?? null), p.categoria && sentenceCase(p.categoria)].filter(Boolean).join(" · ");
  // Las páginas de resultados no se indexan en Google (evita contenido duplicado); las fichas sí.
  return { title: what ? `Resultados: ${what}` : "Todos los repuestos", robots: { index: false, follow: true } };
}

export default async function SearchPage({ searchParams }: { searchParams: Promise<Params> }) {
  const raw = await searchParams;
  const p = {
    q: one(raw.q),
    marca: one(raw.marca),
    modelo: one(raw.modelo),
    anio: yearParam(one(raw.anio)),
    motor: engineParam(one(raw.motor)),
    categoria: one(raw.categoria),
    disponible: one(raw.disponible) === "1",
    importado: one(raw.importado) === "1",
    pagina: Math.max(1, Number(one(raw.pagina)) || 1),
  };

  const base = {
    q: p.q,
    category: p.categoria,
    available: p.disponible ? true : undefined,
    vch: p.importado ? true : undefined,
    page: p.pagina,
    pageSize: 24,
  };
  const year = p.marca && p.modelo && p.anio ? Number(p.anio) : undefined;
  const engine = p.marca && p.modelo ? p.motor : undefined;
  const forVehicle = await searchCatalog({ ...base, brand: p.marca, model: p.marca ? p.modelo : undefined, year, engine });
  // "Mi garaje" no debe esconder repuestos: si con el vehículo no hay nada, se muestran los de todos los
  // vehículos y se avisa (caso real: buscaron "alza cabina dmax" con un Aveo guardado y salía vacío).
  const vehicleFallback = forVehicle.total === 0 && !!p.marca && !!p.q;
  const result = vehicleFallback ? await searchCatalog(base) : forVehicle;

  const href = (changes: Partial<Record<keyof typeof p, string | undefined>>) => {
    const next = new URLSearchParams();
    const merged = { ...p, pagina: undefined, ...changes } as Record<string, unknown>;
    if (merged.disponible === true) merged.disponible = "1";
    if (merged.importado === true) merged.importado = "1";
    for (const [k, v] of Object.entries(merged)) if (v && v !== false) next.set(k, String(v));
    return `/buscar?${next}`;
  };

  const vehicle = p.marca ? [vehicleLabel(p.marca, p.modelo ?? null), year, engine].filter(Boolean).join(" ") : undefined;
  const photo = p.marca && !vehicleFallback ? vehiclePhoto(p.marca, p.modelo) : null;
  const heading = p.q ? `“${p.q}”` : p.categoria ? sentenceCase(p.categoria) : p.importado ? "Importación directa" : vehicle ?? "Todos los repuestos";
  const brands = Object.entries(result.facets.brands ?? {}).sort((a, b) => b[1] - a[1]).slice(0, 12);

  return (
    <div className="flex flex-col gap-5">
      {photo && vehicle && (
        // Como Amazon: el vehículo elegido arriba de los resultados, con su foto.
        <figure className="flex items-center gap-4 overflow-hidden rounded-xl border border-line bg-white p-3">
          <Image src={photo.src} alt={vehicle} width={160} height={100} sizes="160px" priority
            className="h-20 w-32 shrink-0 rounded-lg object-cover sm:h-24 sm:w-40" />
          <figcaption className="min-w-0">
            <p className="text-sm text-muted">Repuestos para tu</p>
            <p className="font-display text-2xl font-bold uppercase leading-tight tracking-wide">{vehicle}</p>
            <Link href="/creditos" className="mt-1 block truncate text-xs text-muted underline-offset-2 hover:underline">
              {photoCredit(photo)}
            </Link>
          </figcaption>
        </figure>
      )}
      <div>
        <h1 className="font-display text-3xl font-bold uppercase tracking-wide">{heading}</h1>
        <p className="text-muted" aria-live="polite">
          {result.total === 0 ? "Sin resultados" : `${result.total.toLocaleString("es-EC")} repuesto${result.total === 1 ? "" : "s"}`}
          {vehicle && p.q && !vehicleFallback ? ` para ${vehicle}` : ""}
        </p>
        {vehicleFallback && result.total > 0 && (
          <p className="mt-2 rounded-lg bg-ask-bg px-3 py-2 text-ask">
            No encontramos esto marcado para tu <strong>{vehicle}</strong>. Te mostramos los de todos los vehículos: revisa
            en cada repuesto para qué carro es, o pregúntanos por WhatsApp.
          </p>
        )}
        {(year || engine) && !vehicleFallback && result.total > 0 && (
          <p className="mt-2 max-w-3xl text-sm text-muted">
            Ocultamos los repuestos que sabemos que son de {year && engine ? "otro año o cilindraje" : year ? "otros años" : "otro cilindraje"}.
            Los que no tienen ese dato registrado sí aparecen: confírmalo al cotizar.
          </p>
        )}
        {result.relaxed && result.total > 0 && (
          <p className="mt-2 rounded-lg bg-ask-bg px-3 py-2 text-ask">
            No encontramos repuestos con todas esas palabras. Te mostramos resultados parecidos.
          </p>
        )}
      </div>

      {/* Filtros activos */}
      {p.importado && !p.q && (
        <p className="max-w-3xl rounded-lg border border-line bg-white px-4 py-3">
          Repuestos que <strong>importamos directamente</strong> (Importadora VCH) desde Corea, Japón y Tailandia. Tienen
          precio al por mayor al llevar $200 o más en estos productos.
        </p>
      )}

      {(p.marca || p.categoria || p.disponible || p.importado) && (
        <ul className="flex flex-wrap gap-2" aria-label="Filtros activos">
          {p.marca && !vehicleFallback && <Chip label={vehicle!} href={href({ marca: undefined, modelo: undefined, anio: undefined, motor: undefined })} />}
          {p.categoria && <Chip label={sentenceCase(p.categoria)} href={href({ categoria: undefined })} />}
          {p.disponible && <Chip label="Solo disponibles" href={href({ disponible: undefined })} />}
          {p.importado && <Chip label="Importación directa" href={href({ importado: undefined })} />}
        </ul>
      )}

      {result.total > 0 && (
        <details className="rounded-xl border border-line bg-white p-3 sm:open:pb-4" open={false}>
          <summary className="cursor-pointer select-none font-semibold">Filtrar resultados</summary>
          <div className="mt-3 flex flex-col gap-4">
            {!p.disponible && (
              <Link href={href({ disponible: "1" })} className="self-start font-semibold underline underline-offset-4">
                Mostrar solo disponibles
              </Link>
            )}
            {!p.importado && (
              <Link href={href({ importado: "1" })} className="self-start font-semibold underline underline-offset-4">
                Solo importación directa
              </Link>
            )}
            {!p.marca && brands.length > 1 && (
              <FacetList title="Marca" items={brands.map(([name, n]) => ({ label: name, count: n, href: href({ marca: name }) }))} />
            )}
          </div>
        </details>
      )}

      {result.total === 0 ? (
        <NoResults query={p.q} vehicle={vehicle} />
      ) : (
        <ProductGrid items={result.items} compat={Object.fromEntries(result.items.map((h) => [h.slug, rowsFromRanges(h.yearRanges)]))} />
      )}

      {result.totalPages > 1 && (
        <nav aria-label="Páginas" className="flex items-center justify-center gap-3">
          {p.pagina > 1 && <PageLink href={href({ pagina: String(p.pagina - 1) })} label="← Anterior" />}
          <span className="text-muted">Página {p.pagina} de {result.totalPages}</span>
          {p.pagina < result.totalPages && <PageLink href={href({ pagina: String(p.pagina + 1) })} label="Siguiente →" />}
        </nav>
      )}
    </div>
  );
}

function Chip({ label, href }: { label: string; href: string }) {
  return (
    <li>
      <Link href={href} className="inline-flex min-h-11 items-center gap-2 rounded-full bg-brand-ink px-4 text-white" aria-label={`Quitar filtro ${label}`}>
        {label} <span aria-hidden="true">✕</span>
      </Link>
    </li>
  );
}

function FacetList({ title, items }: { title: string; items: { label: string; count: number; href: string }[] }) {
  return (
    <div>
      <h2 className="mb-2 text-sm font-bold uppercase tracking-wide text-muted">{title}</h2>
      <ul className="flex flex-wrap gap-2">
        {items.map((i) => (
          <li key={i.label}>
            <Link href={i.href} className="inline-flex min-h-11 items-center gap-2 rounded-full border border-line px-4 hover:border-gray-500">
              {i.label} <span className="text-sm text-muted">{i.count}</span>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}

function PageLink({ href, label }: { href: string; label: string }) {
  return (
    <Link href={href} className="inline-flex min-h-11 items-center rounded-lg border border-line bg-white px-4 font-semibold hover:border-gray-500">
      {label}
    </Link>
  );
}

/** Propuesta de mejora 3: una búsqueda sin resultados se convierte en una consulta por WhatsApp. */
function NoResults({ query, vehicle }: { query?: string; vehicle?: string }) {
  const wa = query ? askLink(query, vehicle) : null;
  return (
    <div className="flex flex-col items-start gap-4 rounded-xl border border-line bg-white p-6">
      <h2 className="font-display text-2xl font-bold uppercase">¿No encontraste tu repuesto?</h2>
      <p className="text-muted">
        Puede que lo tengamos con otro nombre. Escríbenos y te ayudamos a encontrarlo.
      </p>
      {wa && <WhatsAppButton href={wa} label="Preguntar por WhatsApp" />}
      <Link href="/" className="font-semibold underline underline-offset-4">Volver al inicio</Link>
    </div>
  );
}
