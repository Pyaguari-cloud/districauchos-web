import { NextResponse } from "next/server";
import { searchCatalog } from "@/lib/api";
import { isRealCategory } from "@/lib/format";

/**
 * "Repuestos para tu vehículo" de la página Mi garaje: total y categorías con su cantidad para marca/modelo.
 * Solo datos públicos del catálogo (los mismos de la búsqueda); la respuesta se cachea 5 minutos.
 */
export async function GET(req: Request) {
  const url = new URL(req.url);
  const brand = url.searchParams.get("marca")?.slice(0, 40).trim();
  const model = url.searchParams.get("modelo")?.slice(0, 40).trim() || undefined;
  if (!brand) return NextResponse.json({ error: "Falta la marca" }, { status: 400 });
  try {
    const r = await searchCatalog({ brand, model, page: 1, pageSize: 1 });
    const categories = Object.entries(r.facets.categories ?? {})
      .filter(([name]) => isRealCategory(name))
      .sort((a, b) => b[1] - a[1])
      .slice(0, 24)
      .map(([name, count]) => ({ name, count }));
    return NextResponse.json(
      { total: r.total, categories },
      { headers: { "Cache-Control": "public, max-age=300, s-maxage=300" } },
    );
  } catch {
    return NextResponse.json({ error: "No disponible" }, { status: 502 });
  }
}
