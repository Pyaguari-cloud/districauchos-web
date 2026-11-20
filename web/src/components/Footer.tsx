import Link from "next/link";
import { negocio, phoneDisplay } from "@/content/negocio";
import { contactLink } from "@/lib/whatsapp";
import { WhatsAppButton } from "./WhatsAppButton";

export function Footer() {
  const wa = contactLink();
  const link = "underline-offset-2 hover:text-brand-yellow hover:underline";
  return (
    <footer className="mt-12 bg-brand-black text-white">
      <div className="mx-auto grid max-w-6xl gap-8 px-4 py-10 sm:grid-cols-2 lg:grid-cols-4">
        <div className="flex flex-col gap-3">
          <p className="font-display text-xl font-bold uppercase tracking-wide text-brand-yellow">{negocio.name}</p>
          <p className="text-sm text-gray-300">{negocio.tagline} · {negocio.city}, Ecuador</p>
          <p className="text-sm text-gray-300">{negocio.shipping.quito} · envíos a todo el Ecuador</p>
          {wa && <WhatsAppButton href={wa} label="WhatsApp" />}
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
            <li><a href={`mailto:${negocio.email}`} className={link}>{negocio.email}</a></li>
          </ul>
          <ul className="flex gap-4">
            <li><a href={negocio.social.instagram} target="_blank" rel="noopener noreferrer" className={link}>Instagram</a></li>
            <li><a href={negocio.social.tiktok} target="_blank" rel="noopener noreferrer" className={link}>TikTok</a></li>
          </ul>
        </div>
        <p className="mx-auto max-w-6xl px-4 pb-4 text-xs text-gray-400">© {new Date().getFullYear()} {negocio.name}. Precios con IVA incluido.</p>
      </div>
    </footer>
  );
}
