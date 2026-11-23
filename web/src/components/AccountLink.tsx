"use client";

import Link from "next/link";
import { useAccountName } from "@/lib/account";

/** "Ingresar" o "Hola, Juan" en el encabezado. La cuenta es opcional: se puede comprar sin ella. */
export function AccountLink() {
  const name = useAccountName();
  return (
    <Link
      href={name ? "/cuenta" : "/cuenta/ingresar"}
      className="inline-flex h-11 min-w-11 items-center justify-center gap-2 rounded-lg px-2 text-white hover:bg-white/10"
      aria-label={name ? `Mi cuenta (${name})` : "Ingresar o crear cuenta"}
    >
      <svg aria-hidden="true" viewBox="0 0 24 24" className="h-7 w-7 fill-none stroke-current" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
        <circle cx="12" cy="8" r="4" />
        <path d="M4 21c0-4.4 3.6-7 8-7s8 2.6 8 7" />
      </svg>
      <span className="hidden max-w-32 truncate font-display font-bold uppercase tracking-wide sm:inline">
        {name ? `Hola, ${name}` : "Ingresar"}
      </span>
    </Link>
  );
}
