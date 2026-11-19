import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { getVehicles } from "@/lib/api";
import { CartLink } from "./CartLink";
import { HeaderSearch } from "./HeaderSearch";
import { SearchBox } from "./SearchBox";
import { VehicleBar, type VehicleOption } from "./VehicleBar";

/** Barra superior negra con el logo (el logo está diseñado para fondo oscuro). */
const nav = [
  { href: "/buscar?importado=1", label: "Importación directa" },
  { href: "/buscar", label: "Repuestos" },
  { href: "/como-comprar", label: "Cómo comprar" },
  { href: "/locales", label: "Locales" },
  { href: "/nosotros", label: "Nosotros" },
];

export async function Header() {
  // Siempre al pedir la página (nunca en el build): así "Mi garaje" tiene las marcas aunque la página sea estática.
  await connection();
  // Si la API no responde, la página sigue funcionando sin el selector.
  const options: VehicleOption[] = await getVehicles()
    .then((brands) => brands.map((b) => ({ brand: b.brand, models: b.models.map((m) => m.name) })))
    .catch(() => []);

  return (
    <header className="bg-brand-black text-white">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-x-6 gap-y-3 px-4 py-3">
        <Link href="/" className="shrink-0" aria-label="Districauchos, ir al inicio">
          {/* eslint-disable-next-line @next/next/no-img-element -- SVG vectorial liviano (38 KB) */}
          <img src="/brand/logo.svg" alt="Districauchos · Todo en cauchos automotrices" width={220} height={55} className="h-11 w-auto sm:h-12" />
        </Link>
        <div className="order-3 w-full sm:order-2 sm:w-auto sm:flex-1">
          <Suspense fallback={<SearchBox size="sm" />}>
            <HeaderSearch />
          </Suspense>
        </div>
        <div className="order-2 ml-auto sm:order-3 sm:ml-0">
          <CartLink />
        </div>
      </div>
      <nav aria-label="Principal" className="border-t border-white/10">
        <ul className="mx-auto flex max-w-6xl gap-1 overflow-x-auto px-2 sm:px-4">
          {nav.map((n) => (
            <li key={n.href} className="shrink-0">
              <Link href={n.href} className="inline-flex min-h-11 items-center px-3 font-display text-sm font-bold uppercase tracking-wide text-white/85 hover:text-brand-yellow">
                {n.label}
              </Link>
            </li>
          ))}
        </ul>
      </nav>
      {options.length > 0 && (
        <Suspense>
          <VehicleBar options={options} />
        </Suspense>
      )}
    </header>
  );
}
