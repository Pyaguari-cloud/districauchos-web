import { fullAddress, negocio, type Store } from "@/content/negocio";

const SITE_URL = process.env.SITE_URL ?? "https://districauchosvch.com";

/**
 * Ficha de negocio para Google (schema.org AutoPartsStore), una por local.
 * Ayuda a aparecer en "repuestos de caucho Quito" y en Google Maps.
 */
export function storesJsonLd() {
  return negocio.stores.map((s: Store) => ({
    "@context": "https://schema.org",
    "@type": "AutoPartsStore",
    "@id": `${SITE_URL}/locales#${s.id}`,
    name: `${negocio.name} ${s.name}`,
    url: `${SITE_URL}/locales`,
    telephone: `+${s.phone}`,
    email: negocio.email,
    image: `${SITE_URL}/brand/logo-fondo-negro.svg`,
    address: {
      "@type": "PostalAddress",
      streetAddress: s.address,
      addressLocality: negocio.city,
      postalCode: s.postalCode,
      addressCountry: "EC",
    },
    openingHoursSpecification: s.hours.map((h) => ({
      "@type": "OpeningHoursSpecification",
      dayOfWeek: h.days.map((d) => `https://schema.org/${DAY[d]}`),
      opens: h.opens,
      closes: h.closes,
    })),
    hasMap: s.mapsUrl,
    description: fullAddress(s),
  }));
}

export function organizationJsonLd() {
  return {
    "@context": "https://schema.org",
    "@type": "Organization",
    name: negocio.name,
    url: SITE_URL,
    logo: `${SITE_URL}/brand/logo-fondo-negro.svg`,
    email: negocio.email,
    sameAs: Object.values(negocio.social),
  };
}

/** Para incrustar en <script type="application/ld+json"> sin riesgo de cerrar la etiqueta. */
export const jsonLd = (data: unknown) => ({ __html: JSON.stringify(data).replace(/</g, "\\u003c") });

const DAY = { Mo: "Monday", Tu: "Tuesday", We: "Wednesday", Th: "Thursday", Fr: "Friday", Sa: "Saturday", Su: "Sunday" } as const;
