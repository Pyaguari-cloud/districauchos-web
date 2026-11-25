import { contactLink } from "@/lib/whatsapp";
import { WhatsAppIcon } from "./icons";

/**
 * Globo de WhatsApp fijo abajo a la derecha, en todas las páginas (pedido 2026-11-24, como Mansuera). Reemplaza a
 * los botones de WhatsApp sueltos del pie y del menú (se quitaron para no repetir); quedan solo los que cotizan algo
 * concreto (un repuesto, el carrito, "no encontré"). Verde oscuro del sitio: el claro de la marca no contrasta.
 * Respeta la zona segura de los iPhone (barra de inicio) y no tapa el contenido al imprimir.
 */
export function WhatsAppFloat() {
  const wa = contactLink();
  if (!wa) return null;
  return (
    <a href={wa} target="_blank" rel="noopener noreferrer" aria-label="Escríbenos por WhatsApp (se abre en otra pestaña)"
      className="group fixed bottom-[max(1rem,env(safe-area-inset-bottom))] right-4 z-40 flex h-14 w-14 items-center justify-center rounded-full bg-whatsapp text-white shadow-lg shadow-black/30 ring-4 ring-white/80 transition hover:scale-105 hover:bg-whatsapp-dark print:hidden sm:bottom-6 sm:right-6 sm:h-16 sm:w-16">
      <WhatsAppIcon className="h-8 w-8 sm:h-9 sm:w-9" />
      <span aria-hidden="true" className="pointer-events-none absolute right-full mr-3 hidden whitespace-nowrap rounded-lg bg-brand-ink px-3 py-1.5 text-sm font-semibold text-white opacity-0 shadow transition group-hover:opacity-100 sm:block">
        ¿Te ayudamos?
      </span>
    </a>
  );
}
