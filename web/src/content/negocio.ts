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
  /** Del eslogan de su página actual y de su historia (cliente, 2026-11-18). */
  highlights: [
    { value: "+30", label: "años de experiencia" },
    { value: "+10.000", label: "tipos de mangueras" },
    { value: "+8.000", label: "tipos de bujes" },
    { value: "3", label: "locales en Quito" },
  ],
  importOrigins: ["Corea", "Japón", "Tailandia"],
  /** Confirmado por el cliente (2026-11-18). */
  shipping: {
    quito: "Gratis dentro de Quito",
    national: { fee: 5, text: "A todo el Ecuador por cooperativa, con recargo de $5" },
    international: "Envíos internacionales: consúltanos por WhatsApp",
  },
  /** Confirmado por el cliente (2026-11-18). Todas las cotizaciones van al WhatsApp de Cusubamba. */
  payments: {
    payphone: "Tarjeta de crédito o débito con PayPhone",
    transfer: {
      bank: "Banco Pichincha",
      accountType: "Cuenta transaccional",
      number: "2205666408",
      holder: "Enrique Vélez",
    },
  },
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
      mapsUrl: "https://maps.app.goo.gl/zg47QN4bQJ8vh9fP8",
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
      address: "Sector Cusubamba", // ubicación exacta en mapsUrl
      postalCode: "170606",
      phone: "593985595439",
      mapsUrl: "https://maps.app.goo.gl/yFZzpAPsMmkUTU5R6",
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
      mapsUrl: "https://maps.app.goo.gl/FgnGkScW5f6F85Jd6",
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
 * RESPONDIDO (2026-11-18): mapas de los 3 locales, cotizaciones al WhatsApp de Cusubamba, envíos,
 * formas de pago (PayPhone + transferencia) e historia para "Quiénes somos".
 * PENDIENTE:
 * 1. Horarios reales de cada local (la página actual se contradice).
 * 2. ¿Mantener el enlace "Compra y Facturación" (formulario de Google)?
 * 3. PayPhone en la página: ver decisión en el README (enlace de pago vs. pago en el carrito).
 */
