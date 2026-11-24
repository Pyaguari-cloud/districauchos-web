import type { Metadata } from "next";
import Link from "next/link";
import { negocio } from "@/content/negocio";

export const metadata: Metadata = {
  title: "Política de privacidad",
  description: "Qué datos guarda Districauchos VCH, para qué los usa y cómo puedes pedir que se corrijan o se borren.",
};

/**
 * Política de privacidad (Ley Orgánica de Protección de Datos Personales del Ecuador). Escrita 2026-11-23 para
 * las cuentas de clientes. PENDIENTE: que el cliente la revise (y, si puede, un abogado) antes de publicarla.
 */
const UPDATED = "23 de noviembre de 2026";

export default function PrivacyPage() {
  const h2 = "mt-6 font-display text-2xl font-bold uppercase tracking-wide";
  return (
    <article className="mx-auto flex max-w-3xl flex-col gap-3 leading-relaxed">
      <h1 className="font-display text-3xl font-bold uppercase tracking-wide">Política de privacidad</h1>
      <p className="text-sm text-muted">Última actualización: {UPDATED}</p>
      <p>
        En {negocio.name} VCH cuidamos tus datos personales y los usamos solo para atenderte. Esta política explica qué
        datos guardamos, para qué, con quién los compartimos y cómo ejerces tus derechos según la Ley Orgánica de
        Protección de Datos Personales del Ecuador.
      </p>

      <h2 className={h2}>Quién es el responsable</h2>
      <p>
        {negocio.name} VCH, con locales en {negocio.city}, Ecuador. Puedes escribirnos a{" "}
        <a href={`mailto:${negocio.email}`} className="font-semibold underline underline-offset-2">{negocio.email}</a> o
        a cualquiera de nuestros <Link href="/locales" className="font-semibold underline underline-offset-2">locales</Link>.
      </p>

      <h2 className={h2}>Qué datos guardamos</h2>
      <ul className="list-disc pl-6">
        <li><strong>Si creas una cuenta:</strong> tu nombre, correo y contraseña. La contraseña se guarda cifrada: nadie, ni nosotros, puede verla.</li>
        <li><strong>Si los agregas en tu cuenta:</strong> celular, cédula, RUC o pasaporte, dirección y ciudad, y los vehículos de &quot;Mi garaje&quot;.</li>
        <li><strong>Si compras en línea:</strong> los datos del pedido (repuestos, montos, forma de entrega) y los datos para la factura y la entrega.</li>
        <li><strong>Sin cuenta:</strong> el carrito y &quot;Mi garaje&quot; se guardan solo en tu navegador, no en nuestros servidores.</li>
        <li><strong>Si buscas por placa:</strong> la placa y los datos de ese vehículo (marca, modelo, año, tipo y chasis), para no consultarla de nuevo. No pedimos ni guardamos datos del dueño, y la placa no queda ligada a tu nombre ni a tu cuenta.</li>
      </ul>
      <p>No guardamos los datos de tu tarjeta: el pago lo procesa PayPhone en su propia página segura.</p>

      <h2 className={h2}>Para qué los usamos</h2>
      <ul className="list-disc pl-6">
        <li>Mantener tu cuenta, tu garaje y tu historial de pedidos.</li>
        <li>Preparar, facturar y entregar tus pedidos, y contactarte sobre ellos.</li>
        <li>Enviarte correos necesarios, como el enlace para cambiar tu contraseña.</li>
      </ul>
      <p>No vendemos tus datos ni los usamos para publicidad de otras empresas.</p>

      <h2 className={h2}>Con quién los compartimos</h2>
      <p>Solo con los servicios que necesitamos para que la página funcione:</p>
      <ul className="list-disc pl-6">
        <li><strong>PayPhone</strong>, para cobrar los pagos con tarjeta.</li>
        <li><strong>Brevo</strong>, para enviar los correos.</li>
        <li><strong>Consultas.ec</strong>, solo la placa cuando usas &quot;Buscar por placa&quot;, para saber qué vehículo es.</li>
        <li><strong>Cloudflare</strong> y nuestro proveedor de servidor, que guardan y protegen la página. Algunos de estos servicios están fuera del Ecuador.</li>
      </ul>
      <p>También los entregaremos a una autoridad cuando la ley lo exija.</p>

      <h2 className={h2}>Cuánto tiempo los guardamos</h2>
      <p>
        Tu cuenta y sus datos, hasta que la elimines. Los pedidos pagados se guardan el tiempo que exige la ley tributaria,
        aunque elimines tu cuenta, porque los necesitamos para la facturación. Las placas buscadas y los datos de su
        vehículo se guardan sin límite de tiempo, porque no están ligados a ninguna persona.
      </p>

      <h2 className={h2}>Tus derechos</h2>
      <p>
        Puedes pedir en cualquier momento acceder a tus datos, corregirlos, eliminarlos, oponerte a su uso o llevarlos a otro
        servicio. Desde <Link href="/cuenta" className="font-semibold underline underline-offset-2">Mi cuenta</Link> puedes
        corregir tus datos y <strong>eliminar tu cuenta</strong> tú mismo. Para lo demás, escríbenos a {negocio.email} y te
        respondemos dentro del plazo que fija la ley.
      </p>

      <h2 className={h2}>Cookies</h2>
      <p>
        Usamos solo las cookies necesarias para que funcionen tu sesión y el carrito. No usamos cookies de publicidad.
      </p>

      <h2 className={h2}>Cambios</h2>
      <p>Si cambiamos esta política, publicaremos la nueva versión aquí con su fecha.</p>
    </article>
  );
}
