/**
 * Disponibilidad con cantidad (decisión del cliente 2026-11-19, "como Amazon"): el total de todos los locales,
 * nunca en cuál está. Pocas unidades → "¡Solo quedan 2!" en rojo. Sin unidades → el mensaje de siempre.
 */
export const LOW_STOCK = 3;

const units = new Intl.NumberFormat("es-EC", { maximumFractionDigits: 2 });

export function stockText(stock: number) {
  if (stock <= 0) return null;
  if (stock <= LOW_STOCK) return stock === 1 ? "¡Solo queda 1!" : `¡Solo quedan ${units.format(stock)}!`;
  return `${units.format(stock)} disponibles`;
}

export function Availability({ available, stock }: { available: boolean; stock?: number }) {
  const pill = "inline-flex w-fit items-center gap-1.5 self-start rounded-full px-2.5 py-1 text-sm font-semibold";
  // Sin dato de cantidad (no debería pasar): se mantiene el aviso de antes.
  const qty = stock ?? (available ? Infinity : 0);

  if (qty > 0 && qty <= LOW_STOCK) {
    return (
      <span className={`${pill} bg-red-50 text-brand-red`}>
        <span aria-hidden="true" className="h-2 w-2 rounded-full bg-brand-red" />
        {stockText(qty)}
      </span>
    );
  }
  if (qty > 0) {
    return (
      <span className={`${pill} bg-ok-bg text-ok`}>
        <span aria-hidden="true" className="h-2 w-2 rounded-full bg-ok" />
        {Number.isFinite(qty) ? stockText(qty) : "Disponible"}
      </span>
    );
  }
  return (
    <span className={`${pill} bg-ask-bg text-ask`}>
      <span aria-hidden="true" className="h-2 w-2 rounded-full bg-ask" />
      Consultar disponibilidad
    </span>
  );
}
