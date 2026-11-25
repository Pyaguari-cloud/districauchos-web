import { negocio } from "@/content/negocio";
import { InstagramIcon, TikTokIcon } from "./icons";

/**
 * Instagram y TikTok grandes y con el color de cada marca (pedido del cliente 2026-11-24: "están poco visibles").
 * Cada uno es una tarjeta táctil con nombre y usuario. WhatsApp no va aquí: es el globo fijo (WhatsAppFloat).
 */
export function SocialLinks({ compact = false }: { compact?: boolean }) {
  const items = [
    { href: negocio.social.instagram, name: "Instagram", icon: <InstagramIcon className="h-7 w-7" />,
      tile: "bg-[radial-gradient(circle_at_30%_107%,#fdf497_0%,#fdf497_5%,#fd5949_45%,#d6249f_60%,#285aeb_90%)]" },
    { href: negocio.social.tiktok, name: "TikTok", icon: <TikTokIcon className="h-7 w-7 [filter:drop-shadow(-2px_-2px_0_#25f4ee)_drop-shadow(2px_2px_0_#fe2c55)]" />,
      tile: "bg-black ring-1 ring-white/20" },
  ];

  return (
    <ul className={`grid gap-2 grid-cols-2`} aria-label="Síguenos">
      {items.map((i) => (
        <li key={i.name}>
          <a href={i.href} target="_blank" rel="noopener noreferrer"
            className={`group flex items-center gap-3 rounded-xl border border-white/10 bg-white/5 p-2 pr-3 transition hover:border-white/40 hover:bg-white/10 ${compact ? "flex-col gap-1.5 p-3 text-center" : ""}`}>
            <span className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-xl text-white shadow-md transition group-hover:scale-105 ${i.tile}`}>
              {i.icon}
            </span>
            <span className="min-w-0 leading-tight">
              <span className="block font-display text-base font-bold uppercase tracking-wide text-white">{i.name}</span>
            </span>
            <span className="sr-only"> (se abre en otra pestaña)</span>
          </a>
        </li>
      ))}
    </ul>
  );
}
