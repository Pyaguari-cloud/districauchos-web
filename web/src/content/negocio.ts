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
  /** Coordenadas del pin en el mapa de /locales (sacadas del enlace de Maps del cliente, 2026-11-20). */
  lat: number;
  lng: number;
  /** Foto de la fachada en /public/locales/ (fotos propias del cliente, tomadas al abrir los locales; confirmado 2026-11-20). Sin foto, la tarjeta no muestra imagen. */
  photo?: string;
};

const WEEKDAYS: Day[] = ["Mo", "Tu", "We", "Th", "Fr"];

// Confirmado por el cliente (2026-11-18): todos L–V 8–18 y sábado 8–15; solo Cusubamba abre domingo 8–15.
const WEEK: Hours[] = [
  { days: WEEKDAYS, label: "Lunes a viernes", opens: "08:00", closes: "18:00" },
  { days: ["Sa"], label: "Sábado", opens: "08:00", closes: "15:00" },
];

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
    national: { fee: 5, text: "A todo el Ecuador por cooperativa o Servientrega, con recargo de $5 (llega en 24 a 48 horas)" },
    /** Cliente, 2026-11-19: por avión, unos 3 días, recargo de $80. Debe coincidir con ENVIO_GALAPAGOS de la API. */
    galapagos: { fee: 80, text: "A Galápagos por avión, con recargo de $80 (llega en unos 3 días)" },
    international: "Envíos al exterior: consúltanos por WhatsApp",
    /** Cliente, 2026-11-19. */
    dispatch: "Despachamos el mismo día los pedidos de lunes a viernes hasta las 5 de la tarde. Domingos y feriados no se despacha",
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
      lat: -0.2657561,
      lng: -78.551073,
      photo: "/locales/tabiazo.jpg",
      hours: WEEK,
    },
    {
      id: "cusubamba",
      name: "Cusubamba",
      role: "Sucursal",
      address: "Sector Cusubamba", // ubicación exacta en mapsUrl
      postalCode: "170606",
      phone: "593985595439",
      mapsUrl: "https://maps.app.goo.gl/yFZzpAPsMmkUTU5R6",
      lat: -0.2740233,
      lng: -78.5451066,
      photo: "/locales/cusubamba.jpg",
      hours: [...WEEK, { days: ["Su"], label: "Domingo", opens: "08:00", closes: "15:00" }],
    },
    {
      id: "maldonado",
      name: "Maldonado",
      role: "Sucursal",
      address: "Av. Guayanay Ñan y Av. Pedro Vicente Maldonado",
      postalCode: "170146",
      phone: "593999541922",
      mapsUrl: "https://maps.app.goo.gl/FgnGkScW5f6F85Jd6",
      lat: -0.3052964,
      lng: -78.543233,
      photo: "/locales/maldonado.jpg",
      hours: WEEK,
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
 * Todo confirmado por el cliente (2026-11-18): mapas, horarios, WhatsApp de cotizaciones (Cusubamba), envíos,
 * formas de pago e historia. "Compra y Facturación" no va (lleva a su POS interno).
 * PENDIENTE: PayPhone en la página (ver README).
 */
