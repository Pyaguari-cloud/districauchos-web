import { PlateSearch } from "./PlateSearch";
import { SearchBox } from "./SearchBox";
import type { VehicleOption } from "./VehicleBar";

/**
 * "Encuentra el repuesto" del inicio, junto a las promociones (pedido del cliente 2026-11-24, como Mansuera: la
 * búsqueda por placa en grande). Sin pestaña "por nombre": ese buscador ya está en el encabezado, no se repite.
 * Si la búsqueda por placa está apagada (sin clave del proveedor), queda el buscador por nombre.
 * id="buscar": a esto lleva "Buscar por placa" del menú.
 */
export function FinderCard({ options, plates }: { options: VehicleOption[]; plates: boolean }) {
  const benefits = [
    { t: "Repuesto exacto", d: "Para tu marca, modelo y año" },
    { t: "Al instante", d: "Sin esperar a un vendedor" },
    { t: "Compra segura", d: "Tarjeta o WhatsApp" },
  ];

  return (
    <section id="buscar" aria-labelledby="buscar-t" className="flex h-full scroll-mt-4 flex-col gap-4 rounded-2xl bg-brand-ink p-5 text-white sm:p-7">
      <div>
        <h1 id="buscar-t" className="font-display text-3xl font-bold uppercase leading-tight tracking-wide sm:text-4xl">
          {plates ? <>Busca por <span className="text-brand-yellow">tu placa</span></> : <>Encuentra el repuesto <span className="text-brand-yellow">para tu vehículo</span></>}
        </h1>
        <p className="mt-1 text-gray-300">
          {plates
            ? "Escribe la placa y te decimos marca, modelo, año y motor, con los repuestos que le sirven a tu carro."
            : "Busca por nombre, por vehículo o por código OEM."}
        </p>
      </div>

      {plates ? <PlateSearch options={options} /> : <SearchBox size="lg" />}

      <ul className="mt-auto grid grid-cols-3 gap-2 border-t border-white/10 pt-4 text-center">
        {benefits.map((b) => (
          <li key={b.t}>
            <p className="font-display text-sm font-bold uppercase tracking-wide text-brand-yellow sm:text-base">{b.t}</p>
            <p className="text-xs leading-snug text-gray-400">{b.d}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
