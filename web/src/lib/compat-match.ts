/** Lógica compartida (servidor y navegador): ¿una fila de compatibilidad le sirve a "Mi vehículo"? */
export type CompatRow = { brand: string; model: string; engine: string; from: number | null; to: number | null; verified: boolean };

const n = (s: string) => s.toLowerCase().replace(/[^a-z0-9]+/g, " ").trim();

// Versiones de Ecuador que en el garaje están bajo otro nombre de modelo.
const ALIASES: Record<string, string[]> = { "chevy taxi": ["aveo"], "luv d max": ["luv", "d max"], "chevitaxi": ["aveo"] };

/** "Aveo Family" es un "Aveo"; "D-Max RT50" es un "D-Max"; "Grand Vitara" NO es un "Vitara". */
export function sameModel(rowModel: string, garageModel: string) {
  const r = n(rowModel), g = n(garageModel);
  if (r === g || r.startsWith(g + " ")) return true;
  return Object.entries(ALIASES).some(([k, v]) => (r === k || r.startsWith(k + " ")) && v.includes(g));
}

export type Fit =
  | { kind: "none" } // no hay datos de este vehículo
  | { kind: "yes"; rows: CompatRow[]; verified: boolean } // le sirve en ese año
  | { kind: "noYear"; rows: CompatRow[] } // es del modelo, pero la fuente no da años o falta el año del cliente
  | { kind: "otherYears"; rows: CompatRow[] }; // es del modelo, pero de otros años

export function fitFor(rows: CompatRow[], v: { brand: string; model: string | null; year?: number | null } | null): Fit {
  if (!v || !v.model) return { kind: "none" };
  const mine = rows.filter((r) => n(r.brand) === n(v.brand) && sameModel(r.model, v.model!));
  if (!mine.length) return { kind: "none" };
  if (!v.year) return { kind: "noYear", rows: mine };
  const y = v.year;
  const yes = mine.filter((r) => r.from !== null && y >= r.from && y <= (r.to ?? 9999));
  if (yes.length) return { kind: "yes", rows: yes, verified: yes.some((r) => r.verified) };
  if (mine.some((r) => r.from === null)) return { kind: "noYear", rows: mine };
  return { kind: "otherYears", rows: mine };
}

export const yearsText = (r: CompatRow) =>
  r.from === null ? "años por confirmar" : r.to === null ? `desde ${r.from}` : r.from === r.to ? `${r.from}` : `${r.from}–${r.to}`;

/** Rangos de años unidos: "2005–2008 y 2014–2023". */
export function yearRanges(rows: CompatRow[]) {
  const r = rows.filter((x) => x.from !== null).map((x) => [x.from!, x.to ?? 9999] as [number, number]).sort((a, b) => a[0] - b[0]);
  const out: [number, number][] = [];
  for (const [a, b] of r) {
    const last = out[out.length - 1];
    if (last && a <= last[1] + 1) last[1] = Math.max(last[1], b);
    else out.push([a, b]);
  }
  const t = out.map(([a, b]) => (b === 9999 ? `desde ${a}` : a === b ? `${a}` : `${a}–${b}`));
  return t.length > 1 ? `${t.slice(0, -1).join(", ")} y ${t[t.length - 1]}` : (t[0] ?? "");
}

export const rowText = (r: CompatRow) => [r.model, r.engine, yearsText(r)].filter(Boolean).join(" · ");
