import type { Metadata } from "next";
import Link from "next/link";
import { HowItWorks } from "@/components/HowItWorks";
import { negocio } from "@/content/negocio";
import { getOrdersConfig } from "@/lib/orders";

export const metadata: Metadata = {
  title: "Cómo comprar y preguntas frecuentes",
  description: "Cómo cotizar por WhatsApp, envíos gratis en Quito y a todo el Ecuador, formas de pago, precio al por mayor e IVA.",
  alternates: { canonical: "/como-comprar" },
};

// Solo respuestas confirmadas por el cliente (ver content/negocio.ts).
const { shipping, payments } = negocio;
type Faq = { q: string; a: React.ReactNode };
const faq: Faq[] = [
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
    q: "¿Hacen envíos?",
    a: (
      <ul className="list-disc space-y-1 pl-5">
        <li><strong>{shipping.quito}.</strong></li>
        <li>{shipping.national.text}.</li>
        <li>{shipping.galapagos.text}.</li>
        <li>{shipping.dispatch}.</li>
        <li>{shipping.international}.</li>
      </ul>
    ),
  },
  {
    q: "¿Cómo puedo pagar?",
    a: (
      <>
        Cuando confirmamos tu cotización puedes pagar con <strong>{payments.payphone}</strong> o por{" "}
        <strong>transferencia</strong> a nuestra cuenta (ver abajo). También puedes pagar en cualquiera de nuestros locales.
      </>
    ),
  },
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
];

/** La respuesta depende de si el pago en línea está activo (PAYMENTS_MODE en la API). */
function buyOnlineFaq(enabled: boolean, backorderDays: number): Faq {
  return {
    q: "¿Puedo comprar directamente en la página?",
    a: enabled ? (
      <>
        Sí. En el carrito elige cómo lo recibes y toca <strong>“Pagar en línea”</strong>: pagas con tarjeta o con la app
        PayPhone en su página segura. Nosotros no vemos los datos de tu tarjeta.
        {backorderDays > 0 && <> Los repuestos sin stock se venden <strong>bajo pedido</strong> y llegan en aproximadamente {backorderDays} días.</>}
      </>
    ) : (
      <>
        La página es para cotizar: confirmamos disponibilidad y la compra se cierra por WhatsApp o en nuestros{" "}
        <Link href="/locales" className="font-semibold underline underline-offset-2">locales</Link>. No se cobra nada en la página.
      </>
    ),
  };
}

export default async function HowToBuyPage() {
  const config = await getOrdersConfig();
  const items = [...faq, buyOnlineFaq(config.enabled, config.backorderDays)];
  return (
    <div className="flex flex-col gap-8">
      <h1 className="font-display text-3xl font-bold uppercase tracking-wide">Cómo comprar</h1>
      <HowItWorks />
      <section aria-labelledby="faq" className="max-w-3xl">
        <h2 id="faq" className="mb-3 font-display text-2xl font-bold uppercase tracking-wide">Preguntas frecuentes</h2>
        <div className="flex flex-col gap-2">
          {items.map((f) => (
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
      <section aria-labelledby="pago" className="max-w-3xl rounded-xl border border-line bg-white p-4">
        <h2 id="pago" className="mb-2 font-display text-2xl font-bold uppercase tracking-wide">Datos para transferencia</h2>
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1">
          <dt className="font-semibold">Banco</dt><dd>{payments.transfer.bank}</dd>
          <dt className="font-semibold">Tipo</dt><dd>{payments.transfer.accountType}</dd>
          <dt className="font-semibold">Número</dt><dd className="font-mono text-lg">{payments.transfer.number}</dd>
          <dt className="font-semibold">A nombre de</dt><dd>{payments.transfer.holder}</dd>
        </dl>
        <p className="mt-3 text-sm text-muted">Envíanos el comprobante por WhatsApp para despachar tu pedido.</p>
      </section>

      <p className="text-muted">¿Otra pregunta? Escríbenos a {negocio.email} o por WhatsApp.</p>
    </div>
  );
}
