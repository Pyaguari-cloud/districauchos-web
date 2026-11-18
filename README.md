# Districauchos · Catálogo web (Etapa 1)

Catálogo de repuestos que **solo lee** las hojas de Google del cliente y las copia a PostgreSQL para servir la web.
El detalle está en `BRIEF-districauchos-etapa1.md` (privado, no se versiona).

## Estructura

```
data/vehicles.json          Diccionario marca → modelo → alias (versionado; revisar con el cliente)
data/categories.json        Reglas de categorías por inicio de descripción (versionado)
db/init/001_schema.sql      Esquema PostgreSQL (se aplica al crear el contenedor db)
src/
  Districauchos.Core        Reglas puras: limpieza, OEM, stock, precios, vehículos, CatalogBuilder
  Districauchos.Sheets      Lectura de solo lectura: Google Sheets API (spreadsheets.readonly) o CSV locales
  Districauchos.Data        PostgreSQL: snapshot transaccional (TRUNCATE + COPY), sync_runs, price_overrides
  Districauchos.Worker      BackgroundService: sincroniza cada SYNC_INTERVAL_MINUTES
  Districauchos.Search      Índice Meilisearch "products" (carga atómica con índice temporal)
  Districauchos.Api         API pública: /api/catalog, /api/catalog/{slug}, /api/vehicles, /api/categories, /api/health
  Districauchos.Tools       CLI: reporte de vehículos para el cliente
  Dockerfile                Imagen de api/worker (build arg PROJECT)
tests/Districauchos.Core.Tests
web/                        Next.js 16: catálogo público (ver web/README.md)
docker-compose.yml          db, search, worker, api (+ web con --profile web), red districauchos_net
```

## Puesta en marcha

1. `cp .env.example .env` y completar (los IDs de las hojas están en el brief).
2. Clave de la cuenta de servicio en `secrets/google-sa.json`. El cliente comparte los 3 archivos y la carpeta de fotos
   con el correo de la cuenta **solo como lector**.
3. Pruebas: `dotnet test`

### Entregable semana 1: reporte de vehículos

```bash
dotnet run --project src/Districauchos.Tools -- vehicles-report --out out
```

Genera en `out/` (con `;` y UTF-8 con BOM para Excel en español):

- `vehiculos_detectados_<fecha>.csv` — marca, modelo, cuántas fichas (OEM) tiene cada uno
- `sin_vehiculo_<fecha>.csv` — fichas sin vehículo detectado (para completar el diccionario o la descripción)
- `detalle_vehiculos_<fecha>.csv` — cada ficha con su vehículo (para encontrar falsos positivos)
- `categorias_propuestas_<fecha>.csv` — categoría de la hoja vs. categoría propuesta, para que el cliente revise
- `modelos_multimarca_<fecha>.csv` — modelos sin marca en la descripción asignados a todas sus marcas (Vitara, Forza)
- `resumen_<fecha>.txt` — conteos de limpieza, exclusiones y advertencias

Sin cuenta de servicio todavía: exportar cada pestaña a CSV como `samples/<Nombre de pestaña>.csv`
(`Codigos Existentes`, `InventarioTabiazo`, `InventarioCusubamba`, `InventarioMaldonado`, `Maestro Bodega`)
y correr con `LOCAL_CSV_DIR=./samples`.

### Sincronización a la base

```bash
docker compose up -d db                 # Postgres en localhost:5433
dotnet run --project src/Districauchos.Worker
```

## Decisiones tomadas mientras siguen abiertas las preguntas (brief §12)

| Tema | Supuesto actual | Dónde se cambia |
|---|---|---|
| 12.1 Umbral $200 | Sobre todo el carrito, a PVP | `PricingRules.AppliesMayor` |
| 12.1 Excel de sugeridos | Tabla `price_overrides` (solo `precio_mayor` se aplica) | `CatalogBuilder` |
| 12.4 Dos columnas "Stock actual" | **Resuelto 2026-11-18: columna J** (primera que empieza con "Stock actual"; en Cusubamba se llama "STOCK ACTUAL 1") | `SheetLoader.LoadStore` |
| OEM con varios códigos | Separa por `/ , ;`; por espacio solo si cada parte tiene ≥ 5 caracteres | `TextNorm.SplitOems` |
| Códigos con ceros a la izquierda | Se guarda el original; stock se empata ignorando ceros iniciales | `TextNorm.CodeMatchKey` |
| Código repetido en el maestro | Se usa la primera fila y se registra advertencia | `CatalogBuilder` |
| Vitara, Grand Vitara, Forza (Chevrolet = Suzuki) | **Resuelto 2026-11-18:** son el mismo vehículo; siempre se asignan a ambas marcas | `equivalents` en `data/vehicles.json` |
| Categorías (propuesta de mejora 2) | Se asignan por cómo empieza la descripción; si no hay regla, desde la hoja; si no, "Otros". Solo presentación | `data/categories.json`, `CategoryClassifier` |

## Reglas que no se negocian

- Nada en el código escribe en Google: solo `values.get` con scope `spreadsheets.readonly`.
- `precio de COSTO` no se lee ni se guarda.
- Cantidades y ubicaciones de stock solo en `stock_raw` (interna). La API solo expone `available`.
