import type { MetadataRoute } from "next";
import { getSlugs } from "@/lib/api";

const SITE_URL = process.env.SITE_URL ?? "https://districauchosvch.com";

// Se genera al pedirlo (la API no existe durante el build de Docker).
export const dynamic = "force-dynamic";

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const slugs = await getSlugs();
  return [
    { url: `${SITE_URL}/`, changeFrequency: "daily", priority: 1 },
    ...["/locales", "/como-comprar", "/nosotros"].map((p) => ({ url: `${SITE_URL}${p}`, changeFrequency: "monthly" as const, priority: 0.6 })),
    ...slugs.map((s) => ({
      url: `${SITE_URL}/repuesto/${s.slug}`,
      lastModified: s.updatedAt,
      changeFrequency: "weekly" as const,
      priority: 0.8,
    })),
  ];
}
