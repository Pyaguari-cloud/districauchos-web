import "server-only";
import { readFileSync, statSync } from "node:fs";
import type { CompatRow } from "./compat-match";

/**
 * Compatibilidades por OEM (marca, modelo, motor, años), de la hoja que revisa el vendedor.
 * PRUEBA LOCAL: se lee un CSV indicado en COMPAT_FILE. Sin esa variable no hay datos y la web se ve
 * como siempre. Cuando el cliente tenga su hoja de Google, la leerá el Worker (no la web) como el inventario.
 */
let cache: { path: string; mtime: number; map: Map<string, CompatRow[]> } | null = null;

const norm = (oem: string) => oem.toUpperCase().replace(/[^A-Z0-9]/g, "");

function load(): Map<string, CompatRow[]> {
  const path = process.env.COMPAT_FILE;
  if (!path) return new Map();
  try {
    const mtime = statSync(path).mtimeMs;
    if (cache && cache.path === path && cache.mtime === mtime) return cache.map;
    const [head, ...rows] = parseCsv(readFileSync(path, "utf8").replace(/^﻿/, ""));
    const col = (name: string) => head.findIndex((h) => h.toLowerCase().startsWith(name.toLowerCase()));
    const c = {
      oem: col("OEM (inventario)"), clean: col("OEM limpio"), brand: col("Marca"), model: col("Modelo"),
      engine: col("Motor"), from: col("Año desde"), to: col("Año hasta"), verified: col("Verificado"),
    };
    const map = new Map<string, CompatRow[]>();
    for (const r of rows) {
      const verifiedText = (r[c.verified] ?? "").trim().toLowerCase();
      if (verifiedText === "no") continue; // el vendedor dijo que no aplica
      const brands = (r[c.brand] ?? "").split("/").map((s) => s.trim()).filter(Boolean);
      // "Aveo, Aveo Family (L14)" -> ["Aveo", "Aveo Family"]
      const models = (r[c.model] ?? "").replace(/\([^)]*\)/g, "").split(",").map((s) => s.trim()).filter(Boolean);
      if (!brands.length || !models.length) continue;
      const year = (s: string | undefined) => (/^\d{4}$/.test((s ?? "").trim()) ? Number(s!.trim()) : null);
      const base = { engine: (r[c.engine] ?? "").trim(), from: year(r[c.from]), to: year(r[c.to]), verified: verifiedText.startsWith("s") };
      for (const key of new Set([norm(r[c.oem] ?? ""), norm(r[c.clean] ?? "")])) {
        if (!key) continue;
        const list = map.get(key) ?? [];
        for (const brand of brands) for (const model of models) list.push({ brand, model, ...base });
        map.set(key, list);
      }
    }
    cache = { path, mtime, map };
    return map;
  } catch (e) {
    console.error("[compat] no se pudo leer COMPAT_FILE:", e);
    return new Map();
  }
}

/** Filas de compatibilidad de un repuesto (por su OEM y sus alias). */
export function compatFor(oem: string, aliases: string[] = []): CompatRow[] {
  const map = load();
  if (!map.size) return [];
  const seen = new Set<string>();
  const out: CompatRow[] = [];
  for (const code of [oem, ...aliases]) {
    for (const row of map.get(norm(code)) ?? []) {
      const k = JSON.stringify(row);
      if (!seen.has(k)) { seen.add(k); out.push(row); }
    }
  }
  return out;
}

/** Para las tarjetas de búsqueda: slug -> filas. */
export function compatForHits(hits: { slug: string; oem: string }[]): Record<string, CompatRow[]> {
  const out: Record<string, CompatRow[]> = {};
  for (const h of hits) {
    const rows = h.oem ? compatFor(h.oem) : [];
    if (rows.length) out[h.slug] = rows;
  }
  return out;
}

/** CSV con comillas (RFC 4180). */
function parseCsv(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [], field = "", quoted = false;
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (quoted) {
      if (ch === '"' && text[i + 1] === '"') { field += '"'; i++; }
      else if (ch === '"') quoted = false;
      else field += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === ",") { row.push(field); field = ""; }
    else if (ch === "\n" || ch === "\r") {
      if (ch === "\r" && text[i + 1] === "\n") i++;
      row.push(field); field = "";
      if (row.some((f) => f !== "")) rows.push(row);
      row = [];
    } else field += ch;
  }
  row.push(field);
  if (row.some((f) => f !== "")) rows.push(row);
  return rows;
}
