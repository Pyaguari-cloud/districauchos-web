// Prepara fotos de productos para la página: fondo blanco, pieza centrada con margen, 1000×1000 WebP.
//   node web/scripts/fotos-productos.mjs <carpeta-origen> [lista-excluir.txt] [carpeta-destino=media/productos]
// El nombre del archivo es el OEM (con o sin apóstrofos/guiones); se guarda como CLAVE.webp
// (solo letras y números en mayúsculas), que es como el Worker la asocia a la ficha (ProductImages.cs).
// No borra fondos ni marcas de agua: solo recorta el blanco sobrante, centra y comprime.
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";

const require = createRequire(new URL("../package.json", import.meta.url));
const sharp = require("sharp");

const [src, excludeFile, dest = "media/productos"] = process.argv.slice(2);
if (!src) {
  console.error("Uso: node web/scripts/fotos-productos.mjs <carpeta-origen> [lista-excluir.txt] [destino]");
  process.exit(1);
}
const exclude = new Set(
  excludeFile ? fs.readFileSync(excludeFile, "utf8").split(/\r?\n/).map((s) => s.trim()).filter(Boolean) : [],
);
const key = (name) => path.parse(name).name.replace(/[^A-Za-z0-9]/g, "").toUpperCase();

fs.mkdirSync(dest, { recursive: true });
const SIZE = 1000, INNER = 880;
let ok = 0, skipped = 0;
const seen = new Map();
for (const file of fs.readdirSync(src).sort()) {
  if (!/\.(jpe?g|png|webp)$/i.test(file)) continue;
  if (exclude.has(file)) { skipped++; continue; }
  const k = key(file);
  if (!k) { console.warn(`  sin nombre válido: ${file}`); continue; }
  if (seen.has(k)) { console.warn(`  repetida: ${file} y ${seen.get(k)} → ${k}`); continue; }
  seen.set(k, file);
  const piece = await sharp(path.join(src, file))
    .rotate()
    .flatten({ background: "#ffffff" })
    .trim({ background: "#ffffff", threshold: 18 })
    .resize(INNER, INNER, { fit: "inside", withoutEnlargement: false })
    .toBuffer();
  await sharp({ create: { width: SIZE, height: SIZE, channels: 3, background: "#ffffff" } })
    .composite([{ input: piece, gravity: "center" }])
    .webp({ quality: 82 })
    .toFile(path.join(dest, `${k}.webp`));
  ok++;
}
console.log(`Listo: ${ok} fotos en ${dest} (${skipped} excluidas).`);
