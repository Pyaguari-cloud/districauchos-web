# web · Catálogo público (Next.js 16)

Server Components que leen la API .NET (`API_URL`). El navegador nunca habla con la API ni con Meilisearch.

## Desarrollo

```bash
npm install
API_URL=http://localhost:8080 npm run dev      # requiere docker compose up -d db search worker api
npm run lint && npx tsc --noEmit
```

## Variables

| Variable | Uso |
|---|---|
| `API_URL` | URL interna de la API (Docker: `http://api:8080`) |
| `WHATSAPP_NUMBER` | Número del negocio, formato internacional sin `+` (5939XXXXXXXX). Sin él, los botones de WhatsApp no se muestran |
| `SITE_URL` | Dominio público para metadatos, sitemap y canonical |

## Rutas

| Ruta | Qué es | SEO |
|---|---|---|
| `/` | Inicio: buscador, marcas, categorías, más pedidos (clase A) | indexada |
| `/buscar?q=&marca=&modelo=&categoria=&disponible=1&pagina=` | Resultados con filtros | `noindex` (evita duplicados) |
| `/repuesto/[slug]` | Ficha: precio público y por mayor, vehículos, opciones, cotizar por WhatsApp. JSON-LD `Product` **sin precio** (pendiente 12.5) | indexada |
| `/carrito` | Carrito (localStorage) + barra "te faltan $X para por mayor" + enviar cotización por WhatsApp | `noindex` |
| `POST /api/cotizar` | Puente a `POST /api/quote/preview` de la API .NET; agrega el enlace `wa.me`. El navegador solo manda OEM, variante y cantidad | — |
| `/locales`, `/como-comprar`, `/nosotros` | Páginas del negocio. Datos en `src/content/negocio.ts` (un solo archivo; "PENDIENTE" = confirmar con el cliente). `/locales` publica la ficha `AutoPartsStore` de cada local para Google | indexadas |
| `/sitemap.xml`, `/robots.txt` | Fichas + páginas del negocio | — |

## Marca

Logo en `public/brand/` (SVG extraído del PDF vectorial del cliente). Colores en `src/app/globals.css` (`@theme`),
con contrastes verificados. El logo es para fondo oscuro: va siempre sobre la barra negra.

## Decisiones de presentación (solo cambian cómo se ve; la hoja no se toca)

- `displayTitle` quita "(IMPORTADORA VCH)" de los títulos; el slug tampoco lo incluye.
- "Producto Importado" / "Producto Nacional" no se muestran como categorías.
- Precios con punto decimal ($7.00), igual que el mensaje de WhatsApp.

## Vehículo y carrito (semana 3)

- "Mi garaje" (barra bajo el encabezado): hasta 5 vehículos y uno activo, en `localStorage` (`dc.garage.v1`).
  El activo filtra cada búsqueda nueva
  (campos ocultos en el buscador) y en la ficha muestra si el repuesto le sirve.
- Carrito en `localStorage` (`dc.cart.v1`), máximo 50 líneas y 999 unidades. Los precios guardados son solo para
  mostrar mientras carga: **los precios y el mayor los calcula siempre la API** (`QuoteCalculator`).
- Si el navegador bloquea el almacenamiento (modo privado), carrito y vehículo funcionan en memoria.

## Direcciones de la página anterior

`next.config.ts` redirige (308) las URL de WordPress/WooCommerce: `/contactos` → `/locales`, `/tienda` → `/buscar`,
`/producto/<slug>` → `/buscar?q=<slug>`, `/mi-cuenta` y `/finalizar-compra` → `/carrito`.
