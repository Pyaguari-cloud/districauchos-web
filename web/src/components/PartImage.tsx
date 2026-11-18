import { sentenceCase } from "@/lib/format";

/**
 * Foto del repuesto. Mientras no lleguen las fotos de Drive (semana 2, al final),
 * se muestra un marcador con la categoría.
 */
export function PartImage({ image, category, title, large = false }: {
  image: string | null;
  category: string | null;
  title: string;
  large?: boolean;
}) {
  if (image) {
    // eslint-disable-next-line @next/next/no-img-element -- las fotos ya vienen en WebP optimizado
    return <img src={image} alt={title} loading="lazy" className="aspect-square w-full rounded-lg bg-white object-contain" />;
  }
  return (
    <div
      role="img"
      aria-label={`Sin foto: ${title}`}
      className="flex aspect-square w-full flex-col items-center justify-center gap-2 rounded-lg bg-gradient-to-br from-gray-100 to-gray-200 text-gray-400"
    >
      <svg aria-hidden="true" viewBox="0 0 24 24" className={large ? "h-20 w-20" : "h-12 w-12"} fill="none" stroke="currentColor" strokeWidth="1.5">
        <circle cx="12" cy="12" r="9" />
        <circle cx="12" cy="12" r="3.5" />
        <path d="M12 3v5.5M12 15.5V21M3 12h5.5M15.5 12H21" />
      </svg>
      {category && <span className="px-2 text-center text-xs font-semibold uppercase tracking-wide text-gray-500">{sentenceCase(category)}</span>}
    </div>
  );
}
