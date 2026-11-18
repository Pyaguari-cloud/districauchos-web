"use client";

import Link from "next/link";
import { useCart } from "@/lib/cart";

export function CartLink() {
  const count = useCart().length;
  return (
    <Link
      href="/carrito"
      className="relative inline-flex h-11 min-w-11 items-center justify-center gap-2 rounded-lg px-2 text-white hover:bg-white/10"
      aria-label={count ? `Carrito, ${count} producto${count === 1 ? "" : "s"}` : "Carrito vacío"}
    >
      <svg aria-hidden="true" viewBox="0 0 24 24" className="h-7 w-7 fill-none stroke-current" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
        <circle cx="9" cy="20" r="1.5" /><circle cx="18" cy="20" r="1.5" />
        <path d="M2.5 3h2.6l2.4 12h11.2l2-8.5H6.2" />
      </svg>
      <span className="hidden font-display font-bold uppercase tracking-wide sm:inline">Carrito</span>
      {count > 0 && (
        <span className="absolute -right-1 -top-1 min-w-5 rounded-full bg-brand-red px-1.5 text-center text-xs font-bold leading-5 text-white">
          {count}
        </span>
      )}
    </Link>
  );
}
