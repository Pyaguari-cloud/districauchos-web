import Link from "next/link";
import { Suspense } from "react";
import { getVehicles } from "@/lib/api";
import { CartLink } from "./CartLink";
import { HeaderSearch } from "./HeaderSearch";
import { SearchBox } from "./SearchBox";
import { VehicleBar, type VehicleOption } from "./VehicleBar";

/** Barra superior negra con el logo (el logo está diseñado para fondo oscuro). */
export async function Header() {
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
      {options.length > 0 && (
        <Suspense>
          <VehicleBar options={options} />
        </Suspense>
      )}
    </header>
  );
}
