/**
 * Fila de marcas debajo del logo, como en el letrero de los tres locales (pedido del cliente 2026-11-24).
 * Los logos de public/marcas/ vienen a color: aquí se pintan en blanco (como el letrero) con un filtro CSS.
 * Es decorativa: el logo ya dice el nombre del negocio, así que no se lee en voz alta.
 */
const BRANDS = ["chevrolet", "hyundai", "kia", "toyota", "nissan", "mazda", "suzuki", "mitsubishi", "renault", "volkswagen"];

export function BrandStrip({ className = "" }: { className?: string }) {
  return (
    <div aria-hidden="true" className={`flex items-center justify-between gap-1.5 ${className}`}>
      {BRANDS.map((b) => (
        // eslint-disable-next-line @next/next/no-img-element -- SVG pequeños y estáticos
        <img key={b} src={`/marcas/${b}.svg`} alt="" loading="eager" decoding="async"
          className="h-full w-auto min-w-0 flex-1 object-contain opacity-80 [filter:brightness(0)_invert(1)]" />
      ))}
    </div>
  );
}
