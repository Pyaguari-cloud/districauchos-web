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
  | { kind: "image"; id: string; src: string; alt: string; href?: string }
  | { kind: "video"; id: string; src: string; poster?: string; label: string; href?: string };

export const promos: Promo[] = [
  {
    kind: "text", id: "importacion", kicker: "Importación directa VCH",
    title: "Repuestos desde Corea, Japón y", highlight: "Tailandia",
    text: "Precio al por mayor en compras desde $200. Calidad que conocemos porque la importamos nosotros.",
    cta: { label: "Ver importados", href: "/buscar?importado=1" }, image: "/fotos/1784165J00.webp?v=1795265122",
  },
  {
    kind: "text", id: "envios", kicker: "Envíos",
    title: "Gratis dentro de", highlight: "Quito",
    text: "A todo el Ecuador en 24 a 48 horas. Despachamos el mismo día de lunes a viernes hasta las 5 de la tarde.",
    cta: { label: "Cómo comprar", href: "/como-comprar" }, image: "/fotos/486320K010.webp?v=1795265128",
  },
];
