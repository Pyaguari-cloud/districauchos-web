import Link from "next/link";
import { Suspense } from "react";
import { HeaderSearch } from "./HeaderSearch";
import { SearchBox } from "./SearchBox";

/** Barra superior negra con el logo (el logo está diseñado para fondo oscuro). */
export function Header() {
  return (
    <header className="bg-brand-black text-white">
      <div className="mx-auto flex max-w-6xl flex-col gap-3 px-4 py-3 sm:flex-row sm:items-center sm:gap-6">
        <Link href="/" className="shrink-0 self-start sm:self-auto" aria-label="Districauchos, ir al inicio">
          {/* eslint-disable-next-line @next/next/no-img-element -- SVG vectorial liviano (38 KB) */}
          <img src="/brand/logo.svg" alt="Districauchos · Todo en cauchos automotrices" width={220} height={55} className="h-11 w-auto sm:h-12" />
        </Link>
        <div className="flex-1">
          <Suspense fallback={<SearchBox size="sm" />}>
            <HeaderSearch />
          </Suspense>
        </div>
      </div>
    </header>
  );
}
