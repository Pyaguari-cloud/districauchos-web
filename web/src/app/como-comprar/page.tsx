import type { Metadata } from "next";
import Link from "next/link";
import { HowItWorks } from "@/components/HowItWorks";
import { negocio } from "@/content/negocio";

export const metadata: Metadata = {
  title: "Cómo comprar y preguntas frecuentes",
  description: "Cómo cotizar repuestos por WhatsApp en Districauchos, precio al por mayor, IVA y disponibilidad.",
  alternates: { canonical: "/como-comprar" },
};

// Solo respuestas confirmadas. Envíos y formas de pago: pendientes (ver content/negocio.ts).
const faq: { q: string; a: React.ReactNode }[] = [
  {
    q: "¿Cómo cotizo?",
    a: (
      <>
        Busca tus repuestos, agrégalos al carrito y toca <strong>“Enviar cotización por WhatsApp”</strong>. Te llega un
        mensaje listo con los códigos, cantidades y precios. Te respondemos confirmando disponibilidad y precio final.
      </>
    ),
  },
  {
    q: "¿Cómo funciona el precio al por mayor?",
    a: (
      <>
        Los productos <strong>Importadora VCH</strong> tienen precio al por mayor. Se aplica cuando llevas{" "}
        <strong>$200 o más en productos Importadora VCH</strong>, sumados ya a precio por mayor. Los demás productos se
        cobran a precio público. El carrito te muestra cuánto te falta.
      </>
    ),
  },
  { q: "¿Los precios incluyen IVA?", a: "Sí. Todos los precios de la página incluyen IVA." },
  {
    q: "¿Cómo sé si un repuesto le sirve a mi vehículo?",
    a: (
      <>
        Guarda tu vehículo en <strong>“Mi garaje”</strong> (arriba, en cualquier página). Las búsquedas se filtran y en
        cada repuesto verás si figura como compatible. Si tienes dudas, pregúntanos por WhatsApp con el código OEM.
      </>
    ),
  },
  {
    q: "¿Qué significa “Consultar disponibilidad”?",
    a: "Que en este momento no lo tenemos en ningún local. Escríbenos: podemos conseguirlo o sugerirte una alternativa.",
  },
  {
    q: "¿Puedo comprar directamente en la página?",
    a: (
      <>
        La página es para cotizar: la compra se cierra por WhatsApp o en nuestros{" "}
        <Link href="/locales" className="font-semibold underline underline-offset-2">locales</Link>. No se cobra nada en la página.
      </>
    ),
  },
];

export default function HowToBuyPage() {
  return (
    <div className="flex flex-col gap-8">
      <h1 className="font-display text-3xl font-bold uppercase tracking-wide">Cómo comprar</h1>
      <HowItWorks />
      <section aria-labelledby="faq" className="max-w-3xl">
        <h2 id="faq" className="mb-3 font-display text-2xl font-bold uppercase tracking-wide">Preguntas frecuentes</h2>
        <div className="flex flex-col gap-2">
          {faq.map((f) => (
            <details key={f.q} className="group rounded-xl border border-line bg-white p-4 open:shadow-sm">
              <summary className="cursor-pointer list-none font-semibold marker:hidden">
                <span className="flex items-center justify-between gap-4">
                  {f.q}
                  <span aria-hidden="true" className="text-xl transition-transform group-open:rotate-45">+</span>
                </span>
              </summary>
              <div className="mt-2 text-muted">{f.a}</div>
            </details>
          ))}
        </div>
      </section>
      <p className="text-muted">¿Otra pregunta? Escríbenos a {negocio.email} o por WhatsApp.</p>
    </div>
  );
}
