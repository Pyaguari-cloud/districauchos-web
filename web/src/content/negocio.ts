/**
 * Datos del negocio: UN SOLO LUGAR para cambiarlos.
 * Fuente: página actual en WordPress (districauchosvch.com y /contactos), revisada el 2026-11-18.
 * "PENDIENTE" = hay que confirmarlo con el cliente (ver preguntas al final del archivo).
 */

export type Day = "Mo" | "Tu" | "We" | "Th" | "Fr" | "Sa" | "Su";
export type Hours = { days: Day[]; label: string; opens: string; closes: string };

export type Store = {
  id: string;
  name: string;
  role: "Matriz" | "Sucursal";
  address: string;
  postalCode: string;
  phone: string; // formato internacional, solo dígitos
  hours: Hours[];
  /** Enlace de Google Maps del local. PENDIENTE: si falta, se busca por dirección. */
  mapsUrl?: string;
};

const WEEKDAYS: Day[] = ["Mo", "Tu", "We", "Th", "Fr"];

export const negocio = {
  name: "Districauchos",
  tagline: "Todo en cauchos automotrices",
  city: "Quito",
  email: "districauchosich@hotmail.com",
  /** Del eslogan de su página actual. */
  highlights: [
    { value: "+10.000", label: "códigos en mangueras" },
    { value: "+8.000", label: "códigos en bandas y bujes" },
    { value: "3", label: "locales en Quito" },
  ],
  social: {
    instagram: "https://www.instagram.com/distr_icauchosvch",
    tiktok: "https://www.tiktok.com/@districauchos.vch",
  },
  stores: [
    {
      id: "tabiazo",
      name: "Tabiazo",
      role: "Matriz",
      address: "Av. Mariscal Sucre S24-178",
      postalCode: "170606",
      phone: "593986151793",
      // PENDIENTE: su página muestra dos horarios distintos para la matriz (sábado 8:30–13:00 y 8:00–15:00).
      hours: [
        { days: WEEKDAYS, label: "Lunes a viernes", opens: "09:00", closes: "18:00" },
        { days: ["Sa"], label: "Sábado", opens: "08:30", closes: "13:00" },
      ],
    },
    {
      id: "cusubamba",
      name: "Cusubamba",
      role: "Sucursal",
      address: "Sector Cusubamba", // PENDIENTE: dirección exacta
      postalCode: "170606",
      phone: "593985595439",
      hours: [
        { days: WEEKDAYS, label: "Lunes a viernes", opens: "08:00", closes: "18:00" },
        { days: ["Sa", "Su"], label: "Sábado y domingo", opens: "08:00", closes: "15:00" },
      ],
    },
    {
      id: "maldonado",
      name: "Maldonado",
      role: "Sucursal",
      address: "Av. Guayanay Ñan y Av. Pedro Vicente Maldonado",
      postalCode: "170146",
      phone: "593999541922",
      hours: [
        { days: WEEKDAYS, label: "Lunes a viernes", opens: "08:00", closes: "18:00" },
        { days: ["Sa", "Su"], label: "Sábado y domingo", opens: "08:00", closes: "15:00" },
      ],
    },
  ] satisfies Store[],
} as const;

export const fullAddress = (s: Store) => `${s.address}, ${negocio.city} ${s.postalCode}, Ecuador`;
export const mapsLink = (s: Store) =>
  s.mapsUrl ?? `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(`${negocio.name} ${fullAddress(s)}`)}`;
export const phoneDisplay = (p: string) => `+${p.slice(0, 3)} ${p.slice(3, 5)} ${p.slice(5, 8)} ${p.slice(8)}`;
export const storeWhatsApp = (s: Store) =>
  `https://wa.me/${s.phone}?text=${encodeURIComponent(`Hola, les escribo desde la página web (local ${s.name}).`)}`;

/*
 * PREGUNTAS PENDIENTES (2026-11-18):
 * 1. Horarios reales de cada local (la página actual se contradice).
 * 2. Dirección exacta de Cusubamba y enlace de Google Maps de cada local.
 * 3. ¿Cotizaciones a un solo WhatsApp (hoy: Cusubamba) o a cada local?
 * 4. Envíos (a dónde, costo) y formas de pago → se agregan a /como-comprar.
 * 5. ¿Mantener el enlace "Compra y Facturación" (formulario de Google)?
 * 6. Texto de "Quiénes somos" (hoy: borrador en app/nosotros/page.tsx).
 */
