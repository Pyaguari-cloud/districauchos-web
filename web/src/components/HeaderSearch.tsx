"use client";

import { useSearchParams } from "next/navigation";
import { SearchBox } from "./SearchBox";

/** Rellena el buscador del encabezado con la búsqueda actual (?q=). */
export function HeaderSearch() {
  const q = useSearchParams().get("q") ?? "";
  return <SearchBox key={q} defaultValue={q} size="sm" />;
}
