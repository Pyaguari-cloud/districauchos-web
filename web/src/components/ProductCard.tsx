import Link from "next/link";
import type { CatalogHit } from "@/lib/api";
import { displayTitle, money, priceRange } from "@/lib/format";
import { Availability } from "./Availability";
import { PartImage } from "./PartImage";

export function ProductCard({ item }: { item: CatalogHit }) {
  const title = displayTitle(item.title);
  const vehicles = [...item.models.slice(0, 3)];
  return (
    <li className="h-full">
      <Link
        href={`/repuesto/${item.slug}`}
        className="flex h-full flex-col gap-3 rounded-xl border border-line bg-white p-3 transition hover:border-gray-400 hover:shadow-md"
      >
        <div className="relative">
          <PartImage image={item.image} category={item.category} title={title} />
          {item.isVch && (
            <span className="absolute left-2 top-2 whitespace-nowrap rounded bg-brand-red px-1.5 py-0.5 text-[10px] font-bold sm:px-2 sm:text-xs uppercase tracking-wide text-white">
              Importación directa
            </span>
          )}
        </div>
        <div className="flex flex-1 flex-col gap-1.5">
          <h3 className="line-clamp-3 font-semibold leading-snug">{title}</h3>
          <p className="text-sm text-muted">
            OEM <span className="font-mono">{item.oem}</span>
          </p>
          {vehicles.length > 0 && (
            <p className="line-clamp-1 text-sm text-muted">Para: {vehicles.join(", ")}{item.models.length > 3 ? "…" : ""}</p>
          )}
          <div className="mt-auto flex flex-col items-start gap-0.5 pt-2 lg:flex-row lg:flex-wrap lg:items-center lg:justify-between lg:gap-2">
            <span className="font-display text-xl font-bold">{priceRange(item.pvpMin, item.pvpMax)}</span>
            {item.mayorMin !== null && (
              <span className="text-sm font-semibold text-brand-red">Por mayor {money(item.mayorMin)}</span>
            )}
          </div>
          <Availability available={item.available} />
        </div>
      </Link>
    </li>
  );
}

export function ProductGrid({ items }: { items: CatalogHit[] }) {
  return (
    <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
      {items.map((item) => (
        <ProductCard key={item.slug} item={item} />
      ))}
    </ul>
  );
}
