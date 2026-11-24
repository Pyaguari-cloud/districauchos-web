import Link from "next/link";
import { negocio, phoneDisplay } from "@/content/negocio";
import { BrandStrip } from "./BrandStrip";
import { SocialLinks } from "./SocialLinks";

export function Footer() {
  const link = "underline-offset-2 hover:text-brand-yellow hover:underline";
  return (
    <footer className="mt-12 bg-brand-black text-white">
      <div className="mx-auto grid max-w-6xl gap-8 px-4 py-10 sm:grid-cols-2 lg:grid-cols-4">
        <div className="flex flex-col gap-3">
          {/* eslint-disable-next-line @next/next/no-img-element -- SVG vectorial liviano */}
          <img src="/brand/logo.svg" alt={`${negocio.name} · ${negocio.tagline}`} width={1902} height={472} loading="lazy" className="h-14 w-auto self-start" />
          <BrandStrip className="h-4 w-[226px]" />
          <p className="text-sm text-gray-300">{negocio.city}, Ecuador · {negocio.shipping.quito} · envíos a todo el Ecuador</p>
          {/* Pedido del cliente 2026-11-24: WhatsApp, Instagram y TikTok juntos, grandes y con sus colores. */}
          <p className="mt-2 font-display text-sm font-bold uppercase tracking-wider text-white/60">Escríbenos y síguenos</p>
          <SocialLinks />
        </div>

        {negocio.stores.map((s) => (
          <div key={s.id} className="text-sm">
            <p className="font-display text-base font-bold uppercase tracking-wide">
              {s.name} <span className="font-normal text-gray-400">· {s.role}</span>
            </p>
            <p className="text-gray-300">{s.address}</p>
            <p><a href={`tel:+${s.phone}`} className={`${link} inline-block py-1`}>{phoneDisplay(s.phone)}</a></p>
            <ul className="text-gray-300">
              {s.hours.map((h) => <li key={h.label}>{h.label}: {h.opens}–{h.closes}</li>)}
            </ul>
          </div>
        ))}
      </div>
      <div className="border-t border-white/10">
        <div className="mx-auto flex max-w-6xl flex-col gap-3 px-4 py-4 text-sm text-gray-300 sm:flex-row sm:items-center sm:justify-between">
          <ul className="flex flex-wrap gap-x-4 gap-y-1">
            <li><Link href="/buscar" className={link}>Repuestos</Link></li>
            <li><Link href="/como-comprar" className={link}>Cómo comprar</Link></li>
            <li><Link href="/locales" className={link}>Locales</Link></li>
            <li><Link href="/nosotros" className={link}>Nosotros</Link></li>
            <li><Link href="/creditos" className={link}>Créditos de fotos</Link></li>
            <li><Link href="/privacidad" className={link}>Política de privacidad</Link></li>
            <li><a href={`mailto:${negocio.email}`} className={link}>{negocio.email}</a></li>
          </ul>
        </div>
        <p className="mx-auto max-w-6xl px-4 pb-4 text-xs text-gray-400">© {new Date().getFullYear()} {negocio.name}. Precios con IVA incluido.</p>
      </div>
    </footer>
  );
}
