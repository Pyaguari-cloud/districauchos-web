import "server-only";
import { money } from "./format";

/** Número del negocio (WHATSAPP_NUMBER, formato internacional sin "+": 5939XXXXXXXX). */
export function whatsappNumber(): string | null {
  const n = (process.env.WHATSAPP_NUMBER ?? "").replace(/\D/g, "");
  return n.length >= 10 ? n : null;
}

const SITE = "districauchosvch.com";

function link(text: string): string | null {
  const number = whatsappNumber();
  return number ? `https://wa.me/${number}?text=${encodeURIComponent(text)}` : null;
}

/** Cotización de un solo producto desde la ficha (formato del brief §6.7). */
export function quoteOneLink(p: { oem: string; title: string; price: number }) {
  return link(
    ["Hola, quiero cotizar:", `1) ${p.oem} – ${p.title} – Cant: 1 – ${money(p.price)} c/u`, `Enviado desde ${SITE}`].join("\n"),
  );
}

/** Propuesta de mejora 3: búsqueda sin resultados → pregunta directa por WhatsApp. */
export function askLink(query: string, vehicle?: string) {
  const lines = [`Hola, busco: ${query}`];
  if (vehicle) lines.push(`Vehículo: ${vehicle}`);
  lines.push(`Enviado desde ${SITE}`);
  return link(lines.join("\n"));
}

export function contactLink() {
  return link(`Hola, tengo una consulta.\nEnviado desde ${SITE}`);
}
