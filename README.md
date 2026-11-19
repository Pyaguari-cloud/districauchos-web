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
  Districauchos.Api         API pública: /api/catalog, /api/catalog/{slug}, /api/vehicles, /api/categories, /api/health,
                            POST /api/quote/preview, POST /api/admin/sync (Bearer ADMIN_TOKEN)
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

## Forzar sincronización

```bash
curl -X POST -H "Authorization: Bearer $ADMIN_TOKEN" https://<dominio>/api/admin/sync   # 202; máx. 1 por minuto
```
La API avisa al Worker por PostgreSQL (`LISTEN/NOTIFY`, canal `districauchos_sync`); si llega durante una
sincronización, se hace otra al terminar. Resultado en `/api/health`.

## Precios por mayor del Excel de sugeridos

```bash
dotnet run --project src/Districauchos.Tools -- import-prices --file "out/costos y precios sugeridos.xlsx"          # vista previa + reporte
dotnet run --project src/Districauchos.Tools -- import-prices --file "out/costos y precios sugeridos.xlsx" --apply  # guarda y sincroniza
```
Solo lee "ID Producto" y "precio x mayor sugerido" (el costo nunca se lee). Los precios quedan en `price_overrides`
(base de datos, no en la hoja): **volver a correrlo al montar el servidor** o cuando el cliente actualice el Excel.

## Pagos en línea con PayPhone

Decisión del cliente (2026-11-18): el comprador paga directo en la página, como invitado (sin cuenta).
Flujo (Botón de Pagos por redirección, docs.payphone.app):

1. Carrito → `/pagar`: datos de contacto, factura (cédula, RUC, pasaporte o consumidor final) y entrega.
2. `POST /api/orders` (API): recalcula precios, envío y stock, valida los datos, guarda el pedido
   (`orders`, estado `Pending`) y pide a PayPhone el enlace de pago (`api/button/Prepare`).
3. El comprador paga en la página de PayPhone (tarjeta o app) y vuelve a `/pago/respuesta`.
4. `POST /api/orders/confirm`: la API verifica con PayPhone (`api/button/V2/Confirm`) que el pago está
   aprobado, que el monto cuadra y que es este pedido. **Hay 5 minutos**; si no, PayPhone lo reversa solo.
   Recargar la página no confirma dos veces. Si el monto no cuadra queda en `Review` (revisión manual).
5. Aviso al local por correo (`SMTP_*`, `ORDERS_EMAIL_TO`) con todo lo necesario para facturar en su POS,
   y botón para que el comprador avise por WhatsApp a Cusubamba.

- Sin stock: se vende **bajo pedido** (~`BACKORDER_DAYS` días, el comprador lo acepta). `BACKORDER_DAYS=0` lo bloquea.
- Los montos van a PayPhone separando base e IVA (`IVA_RATE`, precios con IVA incluido).
- No se guardan datos de tarjeta. Los datos del comprador solo los lee la API (nunca endpoints públicos).
- `PAYMENTS_MODE`: `off` (solo WhatsApp) · `payphone` · `fake` (simulador en `/pago/prueba`, solo con
  `SITE_URL` en localhost). Probar en local:
  `SITE_URL=http://localhost:3000 PAYMENTS_MODE=fake docker compose --profile web up -d --build api web`

**Para activar el cobro real** (en el servidor, nunca por chat ni en el código):
1. En developer.payphone.app (cuenta Business del cliente) crear la aplicación web y copiar Token y Store ID.
2. En `.env`: `PAYMENTS_MODE=payphone`, `PAYPHONE_TOKEN`, `PAYPHONE_STORE_ID`, `SITE_URL` real y `SMTP_*`.
3. Hacer un pago real pequeño y anularlo desde PayPhone Business.

Pendiente del cliente: confirmar "bajo pedido ~4 días", el límite de consumidor final con su contador
(`CONSUMIDOR_FINAL_MAX`, por defecto $50) y cómo les llega hoy el aviso de venta al WhatsApp.
