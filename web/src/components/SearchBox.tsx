/**
 * Formulario GET a /buscar: funciona sin JavaScript y el resultado se puede compartir por enlace.
 */
export function SearchBox({ defaultValue, size = "lg" }: { defaultValue?: string; size?: "sm" | "lg" }) {
  const big = size === "lg";
  return (
    <form action="/buscar" method="get" role="search" className="flex w-full gap-2">
      <label htmlFor={`q-${size}`} className="sr-only">
        Buscar repuesto por nombre, vehículo o código OEM
      </label>
      <input
        id={`q-${size}`}
        name="q"
        type="search"
        defaultValue={defaultValue}
        placeholder="Ej.: manguera Aveo, 96180071"
        autoComplete="off"
        enterKeyHint="search"
        className={`min-w-0 flex-1 rounded-lg border-2 border-transparent bg-white px-4 text-brand-ink placeholder:text-muted focus:border-brand-yellow focus:outline-none ${
          big ? "h-14 text-lg" : "h-11 text-base"
        }`}
      />
      <button
        type="submit"
        className={`shrink-0 rounded-lg bg-brand-yellow px-5 font-display font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark ${
          big ? "h-14 text-lg" : "h-11"
        }`}
      >
        Buscar
      </button>
    </form>
  );
}
