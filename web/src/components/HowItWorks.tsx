const steps = [
  { n: "1", title: "Busca", text: "Por nombre, por tu vehículo o por código OEM." },
  { n: "2", title: "Agrega al carrito", text: "Elige las cantidades. Ves el precio al instante." },
  { n: "3", title: "Cotiza por WhatsApp", text: "Te llega el detalle listo y te confirmamos disponibilidad." },
];

export function HowItWorks() {
  return (
    <section aria-labelledby="como-funciona">
      <h2 id="como-funciona" className="mb-3 font-display text-2xl font-bold uppercase tracking-wide">Cómo comprar en 3 pasos</h2>
      <ol className="grid gap-3 sm:grid-cols-3">
        {steps.map((s) => (
          <li key={s.n} className="flex gap-3 rounded-xl border border-line bg-white p-4">
            <span aria-hidden="true" className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-brand-yellow font-display text-xl font-bold text-brand-black">
              {s.n}
            </span>
            <div>
              <h3 className="font-display text-lg font-bold uppercase tracking-wide">{s.title}</h3>
              <p className="text-muted">{s.text}</p>
            </div>
          </li>
        ))}
      </ol>
    </section>
  );
}
