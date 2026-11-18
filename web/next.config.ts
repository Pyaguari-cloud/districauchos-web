import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Imagen Docker mínima (web/Dockerfile).
  output: "standalone",
  poweredByHeader: false,
  // Direcciones de la página anterior (WordPress/WooCommerce): no perder visitas de Google ni enlaces guardados.
  async redirects() {
    return [
      { source: "/contactos", destination: "/locales", permanent: true },
      { source: "/tienda", destination: "/buscar", permanent: true },
      { source: "/tienda/:path*", destination: "/buscar", permanent: true },
      { source: "/producto/:slug", destination: "/buscar?q=:slug", permanent: true },
      { source: "/categoria-producto/:path*", destination: "/buscar", permanent: true },
      { source: "/mi-cuenta/:path*", destination: "/carrito", permanent: true },
      { source: "/finalizar-compra/:path*", destination: "/carrito", permanent: true },
    ];
  },
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          { key: "X-Frame-Options", value: "DENY" },
          { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
        ],
      },
    ];
  },
};

export default nextConfig;
