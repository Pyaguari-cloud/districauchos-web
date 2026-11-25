import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { getCategories, getPlateEnabled, getVehicles, vehicleOptions } from "@/lib/api";
import { isRealCategory } from "@/lib/format";
import { AccountLink } from "./AccountLink";
import { CartLink } from "./CartLink";
import { BrandStrip } from "./BrandStrip";
import { HeaderSearch } from "./HeaderSearch";
import { MenuDrawer } from "./MenuDrawer";
import { SearchBox } from "./SearchBox";
import { SocialLinks } from "./SocialLinks";
import { VehicleBar, type VehicleOption } from "./VehicleBar";

/**
 * Barra superior negra con el logo (el logo está diseñado para fondo oscuro). Pedido del cliente 2026-11-24, como
 * Mansuera: menú ☰ a la izquierda, logo más grande y las marcas de carros debajo, como en el letrero de los locales.
 * Las secciones viven solo en el menú (antes había además una fila de enlaces: se quitó para no repetir).
 */
const nav = [
  { href: "/buscar?importado=1", label: "Importación directa" },
  { href: "/buscar", label: "Repuestos" },
  { href: "/como-comprar", label: "Cómo comprar" },
  { href: "/locales", label: "Locales" },
  { href: "/nosotros", label: "Nosotros" },
];
const menuNav = [
  ...nav,
  { href: "/mi-garaje", label: "Mi garaje" },
];

export async function Header() {
  // Siempre al pedir la página (nunca en el build): así "Mi garaje" tiene las marcas aunque la página sea estática.
  await connection();
  // Si la API no responde, la página sigue funcionando sin el selector.
  const [options, categories, plates] = await Promise.all([
    getVehicles().then(vehicleOptions).catch((): VehicleOption[] => []),
    getCategories().then((cs) => cs.map((c) => c.name).filter(isRealCategory).sort((a, b) => a.localeCompare(b, "es"))).catch(() => []),
    getPlateEnabled().catch(() => false),
  ]);

  return (
    <header className="bg-brand-black text-white">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-x-3 gap-y-3 px-4 py-3 sm:gap-x-5">
        <Suspense fallback={<span className="h-11 w-11 shrink-0" />}>
          <MenuDrawer nav={menuNav} categories={categories} plates={plates} social={<SocialLinks compact />} />
        </Suspense>
        <Link href="/" className="flex shrink-0 flex-col gap-1.5" aria-label="Districauchos, ir al inicio">
          {/* eslint-disable-next-line @next/next/no-img-element -- SVG vectorial liviano (38 KB) */}
          <img src="/brand/logo.svg" alt="Districauchos · Todo en cauchos automotrices" width={1902} height={472}
            className="h-12 w-auto sm:h-16" />
          <BrandStrip className="h-3 w-[194px] sm:h-4 sm:w-[258px]" />
        </Link>
        <div className="order-3 w-full md:order-2 md:w-auto md:flex-1">
          <Suspense fallback={<SearchBox size="sm" />}>
            <HeaderSearch />
          </Suspense>
        </div>
        <div className="order-2 ml-auto flex items-center gap-1 md:order-3 md:ml-0">
          <AccountLink />
          <CartLink />
        </div>
      </div>
      {options.length > 0 && (
        <Suspense>
          <VehicleBar options={options} categories={categories} />
        </Suspense>
      )}
    </header>
  );
}
