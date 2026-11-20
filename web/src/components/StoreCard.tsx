import Image from "next/image";
import { fullAddress, mapsLink, negocio, phoneDisplay, storeWhatsApp, type Store } from "@/content/negocio";

/** Tarjeta de un local: dirección, horario y botones grandes (≥ 44 px) para llamar, escribir y llegar. */
export function StoreCard({ store, compact = false }: { store: Store; compact?: boolean }) {
  const btn = "inline-flex min-h-11 flex-1 items-center justify-center whitespace-nowrap rounded-lg px-3 font-display font-bold uppercase tracking-wide";
  return (
    <article className="flex flex-col gap-3 rounded-xl border border-line bg-white p-4" aria-labelledby={`local-${store.id}`}>
      {!compact && store.photo && (
        <Image
          src={store.photo}
          alt={`Fachada del local ${store.name} de ${negocio.name}`}
          width={408}
          height={255}
          sizes="(min-width: 768px) 33vw, 100vw"
          className="-mx-4 -mt-4 aspect-[8/5] w-[calc(100%+2rem)] max-w-none rounded-t-xl object-cover object-top"
        />
      )}
      <div>
        <p className="text-sm font-semibold uppercase tracking-wide text-brand-red">{store.role}</p>
        <h3 id={`local-${store.id}`} className="font-display text-2xl font-bold uppercase tracking-wide">{store.name}</h3>
        <p className="text-muted">{store.address}, {negocio.city}</p>
      </div>
      {!compact && (
        <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
          {store.hours.map((h) => (
            <div key={h.label} className="contents">
              <dt className="font-semibold">{h.label}</dt>
              <dd>{h.opens} a {h.closes}</dd>
            </div>
          ))}
        </dl>
      )}
      <div className="mt-auto flex flex-wrap gap-2">
        <a href={`tel:+${store.phone}`} className={`${btn} border-2 border-brand-black hover:bg-brand-yellow`} aria-label={`Llamar al local ${store.name}, ${phoneDisplay(store.phone)}`}>
          Llamar
        </a>
        <a href={storeWhatsApp(store)} target="_blank" rel="noopener noreferrer" className={`${btn} bg-whatsapp text-white hover:bg-whatsapp-dark`}>
          WhatsApp
        </a>
        <a href={mapsLink(store)} target="_blank" rel="noopener noreferrer" className={`${btn} bg-brand-yellow text-brand-black hover:bg-brand-yellow-dark`} aria-label={`Cómo llegar al local ${store.name}: ${fullAddress(store)}`}>
          Cómo llegar
        </a>
      </div>
      <p className="text-sm text-muted">Tel. {phoneDisplay(store.phone)}</p>
    </article>
  );
}
