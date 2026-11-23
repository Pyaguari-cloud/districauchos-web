import type { MetadataRoute } from "next";

const SITE_URL = process.env.SITE_URL ?? "https://districauchosvch.com";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: { userAgent: "*", allow: "/", disallow: ["/buscar", "/cuenta", "/api"] },
    sitemap: `${SITE_URL}/sitemap.xml`,
  };
}
