import { FERIADOS } from "@/content/feriados";
import type { Delivery } from "./quote";

/**
 * Fecha estimada de entrega "como Amazon". Reglas del cliente (2026-11-19):
 * - Despacho el mismo día si se pide de lunes a viernes antes de las 17:00 (hora de Ecuador).
 * - Domingos y feriados no se despacha: pasa al siguiente día laborable (el sábado sí se despacha).
 * - Quito: llega el mismo día del despacho. Provincia (cooperativa o Servientrega): 24 a 48 horas.
 *   Galápagos: 3 días por avión. Bajo pedido: se suman los días de espera del proveedor.
 * Es una estimación para mostrar; no es una promesa contractual.
 */
export const CUTOFF_HOUR = 17;
const ECUADOR_OFFSET_H = -5; // Ecuador no tiene horario de verano

const HOLIDAYS = new Set(FERIADOS);

/** Día "civil" de Ecuador como fecha UTC a medianoche (para sumar días sin problemas de zona horaria). */
type Day = Date;

function ecuadorNow(now: Date): { day: Day; hour: number } {
  const local = new Date(now.getTime() + ECUADOR_OFFSET_H * 3600_000);
  return {
    day: new Date(Date.UTC(local.getUTCFullYear(), local.getUTCMonth(), local.getUTCDate())),
    hour: local.getUTCHours(),
  };
}

const iso = (d: Day) => d.toISOString().slice(0, 10);
const addDays = (d: Day, n: number) => new Date(d.getTime() + n * 86_400_000);
/** Día en que se puede despachar o entregar: no domingo ni feriado. */
export const isWorkingDay = (d: Day) => d.getUTCDay() !== 0 && !HOLIDAYS.has(iso(d));

function nextWorkingDay(d: Day): Day {
  let x = addDays(d, 1);
  while (!isWorkingDay(x)) x = addDays(x, 1);
  return x;
}

function addWorkingDays(d: Day, n: number): Day {
  let x = d;
  for (let i = 0; i < n; i++) x = nextWorkingDay(x);
  return x;
}

/** Día de despacho para un pedido hecho en `now`. */
export function dispatchDay(now: Date): Day {
  const { day, hour } = ecuadorNow(now);
  const weekday = day.getUTCDay(); // 0 = domingo … 6 = sábado
  const sameDay = weekday >= 1 && weekday <= 5 && hour < CUTOFF_HOUR && isWorkingDay(day);
  return sameDay ? day : nextWorkingDay(day);
}

export type Eta = { from: Day; to: Day; today: Day };

export function estimate(delivery: Exclude<Delivery, "none">, now: Date, backorderDays = 0): Eta {
  const today = ecuadorNow(now).day;
  let start = dispatchDay(now);
  if (backorderDays > 0) start = addWorkingDays(start, backorderDays);
  switch (delivery) {
    case "pickup":
    case "quito":
      return { from: start, to: start, today };
    case "national":
      return { from: addWorkingDays(start, 1), to: addWorkingDays(start, 2), today };
    case "galapagos":
      return { from: addWorkingDays(start, 3), to: addWorkingDays(start, 3), today };
  }
}

const WEEKDAY = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];
const MONTH = ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

/** "hoy", "mañana" o "el jueves 20 de noviembre" (sin el mes si `withMonth` es false). */
export function dayText(d: Day, today: Day, withMonth = true): string {
  const diff = Math.round((d.getTime() - today.getTime()) / 86_400_000);
  if (diff === 0) return "hoy";
  if (diff === 1) return "mañana";
  return `el ${WEEKDAY[d.getUTCDay()]} ${d.getUTCDate()}${withMonth ? ` de ${MONTH[d.getUTCMonth()]}` : ""}`;
}

/** Texto corto: "Llega hoy", "Llega entre mañana y el sábado 22 de noviembre", "Listo para retirar mañana". */
export function etaText(delivery: Exclude<Delivery, "none">, eta: Eta): string {
  const verb = delivery === "pickup" ? "Listo para retirar" : "Llega";
  if (eta.from.getTime() === eta.to.getTime()) return `${verb} ${dayText(eta.from, eta.today)}`;
  // "entre el sábado 21 y el lunes 23 de noviembre": el mes una sola vez si es el mismo.
  const sameMonth = eta.from.getUTCMonth() === eta.to.getUTCMonth();
  return `${verb} entre ${dayText(eta.from, eta.today, !sameMonth)} y ${dayText(eta.to, eta.today)}`;
}

/** Minutos que faltan para el corte de hoy (null si hoy ya no sale el mismo día). Para "pide en 2 h 15 min". */
export function minutesToCutoff(now: Date): number | null {
  const { day, hour } = ecuadorNow(now);
  if (dispatchDay(now).getTime() !== day.getTime()) return null;
  const local = new Date(now.getTime() + ECUADOR_OFFSET_H * 3600_000);
  return (CUTOFF_HOUR - hour) * 60 - local.getUTCMinutes();
}
