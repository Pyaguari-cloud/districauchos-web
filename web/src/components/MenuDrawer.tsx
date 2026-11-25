"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { useEffect, useRef } from "react";
import { sentenceCase } from "@/lib/format";
import { ChevronIcon, CloseIcon, MenuIcon, PlateIcon } from "./icons";

export type NavItem = { href: string; label: string };

/**
 * Menú lateral (☰ a la izquierda), como el de Mansuera al que los clientes ya están acostumbrados (pedido
 * 2026-11-24). Es un <dialog> nativo: atrapa el foco, se cierra con Esc, tocando fuera o al navegar, y el lector de
 * pantalla lo anuncia como diálogo. Sin JavaScript el botón no hace nada, pero el sitio igual tiene enlaces abajo.
 */
/** social: los botones de redes ya armados en el servidor (SocialLinks lee el número de WhatsApp, que es del servidor). */
export function MenuDrawer({ nav, categories, plates, social }: {
  nav: NavItem[]; categories: string[]; plates: boolean; social: React.ReactNode;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const pathname = usePathname();
  const params = useSearchParams();

  const open = () => {
    dialog.current?.showModal();
    document.documentElement.style.overflow = "hidden"; // que no se mueva la página de atrás
  };
  const close = () => dialog.current?.close();

  // Al navegar (tocar un enlace) se cierra solo.
  useEffect(() => { close(); }, [pathname, params]);

  const row = "flex min-h-12 items-center justify-between gap-3 border-b border-white/10 px-4 font-semibold text-white hover:bg-white/5 hover:text-brand-yellow";

  return (
    <>
      <button type="button" onClick={open} aria-haspopup="dialog"
        className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full border border-white/20 text-white hover:border-brand-yellow hover:text-brand-yellow">
        <MenuIcon />
        <span className="sr-only">Abrir menú</span>
      </button>

      <dialog ref={dialog} aria-label="Menú"
        onClose={() => { document.documentElement.style.overflow = ""; }}
        // Cierra al tocar fuera o cualquier enlace (también "/#buscar" estando ya en el inicio: ahí no cambia la ruta).
        onClick={(e) => { if (e.target === dialog.current || (e.target as HTMLElement).closest("a")) close(); }}
        className="m-0 h-dvh max-h-none w-[min(22rem,88vw)] max-w-none overflow-hidden border-0 bg-brand-ink p-0 text-white [color-scheme:dark] shadow-2xl backdrop:bg-black/60 open:animate-[drawer-in_.2s_ease-out] motion-reduce:open:animate-none">
        <div className="flex h-full flex-col overflow-y-auto">
          <div className="sticky top-0 z-10 flex items-center justify-between gap-3 bg-brand-black px-4 py-3">
            <p className="font-display text-lg font-bold uppercase tracking-wide text-brand-yellow">Menú</p>
            <button type="button" onClick={close}
              className="flex h-11 w-11 items-center justify-center rounded-full text-white hover:bg-white/10">
              <CloseIcon />
              <span className="sr-only">Cerrar menú</span>
            </button>
          </div>

          {plates && (
            <div className="p-4">
              <Link href="/#buscar" className="flex min-h-12 items-center justify-center gap-2 rounded-lg bg-brand-yellow px-4 font-display text-lg font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark">
                <PlateIcon className="h-6 w-6" /> Buscar por placa
              </Link>
            </div>
          )}

          <nav aria-label="Secciones">
            <ul>
              {nav.map((n) => (
                <li key={n.href}><Link href={n.href} className={row}>{n.label}<ChevronIcon className="h-4 w-4 opacity-50" /></Link></li>
              ))}
            </ul>
          </nav>

          {categories.length > 0 && (
            <nav aria-labelledby="menu-cat" className="mt-4">
              <p id="menu-cat" className="px-4 pb-1 font-display text-sm font-bold uppercase tracking-wider text-white/60">Categorías</p>
              <ul>
                {categories.map((c) => (
                  <li key={c}>
                    <Link href={`/buscar?categoria=${encodeURIComponent(c)}`} className={`${row} font-normal`}>
                      {sentenceCase(c)}<ChevronIcon className="h-4 w-4 opacity-50" />
                    </Link>
                  </li>
                ))}
              </ul>
            </nav>
          )}

          <div className="mt-auto p-4 pt-6">
            <p className="pb-2 font-display text-sm font-bold uppercase tracking-wider text-white/60">Síguenos</p>
            {social}
          </div>
        </div>
      </dialog>
    </>
  );
}
