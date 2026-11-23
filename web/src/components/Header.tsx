import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { getCategories, getVehicles, vehicleOptions } from "@/lib/api";
import { isRealCategory } from "@/lib/format";
import { AccountLink } from "./AccountLink";
import { CartLink } from "./CartLink";
import { HeaderSearch } from "./HeaderSearch";
import { SearchBox } from "./SearchBox";
import { VehicleBar, type VehicleOption } from "./VehicleBar";

/** Barra superior negra con el logo (el logo está diseñado para fondo oscuro). */
const nav = [
  { href: "/buscar?importado=1", label: "Importación directa", short: "Importados" },
  { href: "/buscar", label: "Repuestos" },
  { href: "/como-comprar", label: "Cómo comprar" },
  { href: "/locales", label: "Locales" },
  { href: "/nosotros", label: "Nosotros" },
];

export async function Header() {
  // Siempre al pedir la página (nunca en el build): así "Mi garaje" tiene las marcas aunque la página sea estática.
  await connection();
  // Si la API no responde, la página sigue funcionando sin el selector.
  const [options, categories] = await Promise.all([
    getVehicles().then(vehicleOptions).catch((): VehicleOption[] => []),
    getCategories().then((cs) => cs.map((c) => c.name).filter(isRealCategory).sort((a, b) => a.localeCompare(b, "es"))).catch(() => []),
  ]);

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
        <div className="order-2 ml-auto flex items-center gap-1 sm:order-3 sm:ml-0">
          <AccountLink />
          <CartLink />
        </div>
      </div>
      <nav aria-label="Principal" className="border-t border-white/10">
        <ul className="mx-auto flex max-w-6xl justify-between overflow-x-auto px-1 sm:justify-center sm:gap-2 sm:px-4">
          {nav.map((n) => (
            <li key={n.href} className="shrink-0">
              <Link href={n.href} className="inline-flex min-h-11 items-center px-1.5 font-display text-[13px] font-bold uppercase tracking-wide text-white/85 hover:text-brand-yellow sm:px-3 sm:text-sm">
                {n.short ? (<><span className="sm:hidden">{n.short}</span><span className="hidden sm:inline">{n.label}</span></>) : n.label}
              </Link>
            </li>
          ))}
        </ul>
      </nav>
      {options.length > 0 && (
        <Suspense>
          <VehicleBar options={options} categories={categories} />
        </Suspense>
      )}
    </header>
  );
}
