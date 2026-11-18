/** Nunca se muestran cantidades ni ubicaciones (brief §6.3). */
export function Availability({ available }: { available: boolean }) {
  return available ? (
    <span className="inline-flex w-fit items-center gap-1.5 self-start rounded-full bg-ok-bg px-2.5 py-1 text-sm font-semibold text-ok">
      <span aria-hidden="true" className="h-2 w-2 rounded-full bg-ok" />
      Disponible
    </span>
  ) : (
    <span className="inline-flex w-fit items-center gap-1.5 self-start rounded-full bg-ask-bg px-2.5 py-1 text-sm font-semibold text-ask">
      <span aria-hidden="true" className="h-2 w-2 rounded-full bg-ask" />
      Consultar disponibilidad
    </span>
  );
}
