/**
 * Pantalla de ofertas y comunicados del inicio (pedido del cliente 2026-11-24, como Mansuera). UN SOLO LUGAR para
 * cambiarlas. Tres tipos:
 *  - "text":  la armamos nosotros con los colores de la marca (título, texto, botón y una foto de producto opcional).
 *  - "image": un banner ya diseñado por el cliente (se ve completo, sin recortar). Va en public/promos/.
 *  - "video": video publicitario o de productos en promoción. Va sin sonido y en bucle (los navegadores no dejan
 *             reproducir solos los videos con sonido); hay un botón para activar el sonido. Ideal MP4 (H.264), menos
 *             de 15 MB, horizontal 16:9 o cuadrado, y un "poster" (imagen del primer cuadro) para que cargue rápido.
 * El orden de la lista es el orden en pantalla.
 */
export type Promo =
  | { kind: "text"; id: string; kicker?: string; title: string; highlight?: string; text: string; cta: { label: string; href: string }; image?: string }
  | { kind: "image"; id: string; src: string; alt: string; href?: string; cta?: string }
  | { kind: "video"; id: string; src: string; poster?: string; label: string; href?: string; cta?: string };

// Material del cliente (2026-11-24). Videos: recortados y comprimidos con FFmpeg desde out/videos/ (la importación es
// del "video estándares de calidad", segundos 24 a 56, sin las barras negras). Banners: capturas de Instagram, recortadas
// para quitar el ícono de silencio que traían. Si el cliente manda los originales, reemplazar los archivos.
export const promos: Promo[] = [
  {
    kind: "video", id: "importacion", src: "/promos/importacion.mp4", poster: "/promos/importacion.webp",
    label: "Video: llegada y descarga de nuestra importación directa", href: "/buscar?importado=1", cta: "Ver importados",
  },
  {
    kind: "image", id: "hilux-buje", src: "/promos/hilux-buje.webp",
    alt: "Buje de tijera inferior delantera para Toyota Hilux Vigo 4x2, código 48655-0K010",
    href: "/buscar?q=48655-0K010", cta: "Ver buje Hilux",
  },
  {
    kind: "image", id: "mangueras", src: "/promos/mangueras-radiador.webp",
    alt: "Mangueras de radiador con refuerzo textil, resistentes a altas temperaturas, y abrazaderas de acero inoxidable",
    href: "/buscar?q=manguera%20radiador", cta: "Ver mangueras",
  },
  {
    kind: "image", id: "sail-guardapolvo", src: "/promos/sail-guardapolvo.webp",
    alt: "Guardapolvo de eje izquierdo y derecho para Chevrolet Aveo y Sail, código 96391553",
    href: "/buscar?q=96391553", cta: "Ver guardapolvo",
  },
  {
    kind: "video", id: "locales", src: "/promos/locales.mp4", poster: "/promos/locales.webp",
    label: "Video: horarios y nuestros tres locales en Quito", href: "/locales", cta: "Ver locales",
  },
  {
    kind: "image", id: "estabilizadoras", src: "/promos/estabilizadoras.webp",
    alt: "Cauchos de barra estabilizadora para Chevrolet LUV, Chevette, Sprint, Spark, LUV D-Max y caja de dirección de Aveo y Cronos",
    href: "/buscar?q=estabilizadora", cta: "Ver estabilizadoras",
  },
];
