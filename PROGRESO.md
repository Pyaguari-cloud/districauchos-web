# Districauchos · Avance del proyecto

Bitácora para retomar el trabajo en cualquier momento. **Leer primero esto**; el detalle técnico está en
`README.md` y `web/README.md`, y el encargo original en `BRIEF-districauchos-etapa1.md` (privado, solo local).

Última actualización: 2026-11-19

---

## 1. Qué es

Página web de **Districauchos VCH** (Quito, 3 locales + bodega, repuestos de caucho automotriz):

- un catálogo que se alimenta solo de las hojas de Google del cliente;
- carrito con precio por mayor;
- cotización por WhatsApp y pago en línea con PayPhone;
- "Mi garaje" para filtrar por vehículo;
- las páginas del negocio: locales, cómo comprar y nosotros.

Reemplaza la tienda WordPress/WooCommerce actual en `districauchosvch.com`.

**Stack:**
- datos y servidor: .NET 8 (API y Worker), PostgreSQL 16 y Meilisearch;
- web: Next.js 16;
- todo corre en Docker Compose.

## 2. Reglas que no se negocian

1. **Las hojas de Google del cliente solo se LEEN.** Nunca se escribe, borra ni modifica nada en ellas.
2. **Nunca se muestran** costos, cantidades de stock ni en qué local está cada repuesto. Solo "Disponible", "Bajo pedido" o "Consultar".
3. **Los secretos van solo en `.env`**, que nunca se sube a git. Son tokens (PayPhone, Meilisearch, admin), contraseñas y la cuenta de Google en `secrets/`.
4. **Estos archivos no se suben a git:**
   - `BRIEF-*.md`, con los IDs de las hojas;
   - `out/`, con los archivos del cliente: el Excel con costos, el PDF y las capturas;
   - `samples/` y `media/`.
5. **El repositorio de GitHub tiene que ser PRIVADO.**
6. **Del Excel "costos y precios sugeridos" solo se leen** `ID Producto` y `precio x mayor sugerido`. La columna de costo nunca se lee.
7. **Nadie pega tokens ni contraseñas en el chat.** Se ponen directamente en `.env`.

## 3. Estado por etapas

| Etapa | Estado | Qué incluye |
|---|---|---|
| Semana 1: datos | ✅ | Lectura de las 5 pestañas, limpieza, OEM, stock total (locales + bodega), vehículos, reporte para el cliente |
| Semana 2: buscador y web | ✅ | Meilisearch, API pública, web Next.js, fichas, SEO, redirecciones de las URLs viejas de WordPress |
| Mejoras 2 y 5 | ✅ | Categorías automáticas (`data/categories.json`) y títulos legibles |
| Semana 3: vehículo, carrito y cotización | ✅ | Mi garaje (hasta 5 vehículos), carrito, cotización por WhatsApp calculada en el servidor |
| Página del negocio | ✅ | Inicio, /locales, /como-comprar (FAQ), /nosotros, pie de página, datos estructurados para Google |
| Importación directa VCH | ✅ | Productos VCH primero en el buscador, sección en el inicio, filtro `/buscar?importado=1` y etiqueta |
| Envíos | ✅ | Retiro, Quito gratis, provincia +$5 por cooperativa (`ENVIO_NACIONAL`) |
| Pago en línea PayPhone | 🟢 Conexión con PayPhone OK (token Web) | Falta el cobro real de $1 en el servidor (ver §6) |
| Semana 4: servidor y lanzamiento | ⏳ Pendiente | VPS, Cloudflare, DNS, `.env` de producción, prueba real de $1 |

Pruebas: 139 en `tests/` (`dotnet test`). Lint de la web: `cd web && npx tsc --noEmit && npx eslint .`

## 4. Decisiones del cliente (confirmadas)

**Precio por mayor**
- Solo para productos **Importadora VCH**.
- Se aplica cuando la suma de productos VCH **a precio por mayor** llega a **$200 o más**.
- El resto se cobra a precio público.
- Precio por mayor: el del Excel de sugeridos si está; si no, el 40% de descuento (`MAYOR_DESCUENTO`).

**Precios e impuestos**
- Los precios de la hoja **ya incluyen IVA**.

**Cotizaciones y avisos**
- Todas las cotizaciones y avisos de venta van al **WhatsApp de Cusubamba**: +593 98 559 5439.
- Aviso de cada venta pagada por **correo a districauchosich@hotmail.com** (confirmado). Hoy por WhatsApp les llega solo "tenemos un nuevo pedido confirmado" con los datos del cliente y lo pedido; el correo nuestro trae eso y más.

**Envíos**
- Quito gratis.
- Provincia +$5 por cooperativa.
- Internacional: se consulta.

**Pagos**
- **PayPhone directo en la página**: el comprador paga como invitado, sin cuenta.
- También **transferencia** al Banco Pichincha (los datos están en `web/src/content/negocio.ts`).

**Sin stock**
- Se puede pagar como **"Bajo pedido, llega en ~4 días"**, y el comprador lo acepta antes de pagar (`BACKORDER_DAYS=4`). **Confirmado por el cliente (2026-11-18).**
- Los importados VCH están en bodega.

**Factura**
- **Siempre con cédula, RUC o pasaporte**, sea cual sea el monto. No hay consumidor final.
- El local factura en **su POS**, que envía la factura por WhatsApp o correo. La página no factura: solo pasa los datos.

**Horarios**
- Los tres locales: lunes a viernes 8:00–18:00 y sábado 8:00–15:00.
- Domingo 8:00–15:00 **solo Cusubamba**.

**Otros**
- "Compra y Facturación" **no va** en la página: es su POS interno.
- **Mapas:** Tabiazo (matriz, Av. Mariscal Sucre), Cusubamba y Maldonado. Los enlaces están en `negocio.ts`.
- **Historia:** +30 años, importadores directos de Corea, Japón y Tailandia, +10.000 mangueras, +8.000 bujes.

Todo el contenido del negocio está en un solo archivo: **`web/src/content/negocio.ts`**.

## 5. Cómo se paga (resumen)

1. `/carrito` → `/pagar`: el comprador llena contacto, factura y entrega.
2. La API recalcula todo y guarda el pedido en la tabla `orders`.
3. La API pide el enlace a PayPhone y el comprador paga en la página de PayPhone.
4. PayPhone lo devuelve a `/pago/respuesta`.
5. La API **confirma con PayPhone**. Hay 5 minutos para hacerlo; si no, PayPhone reversa el cobro solo.
6. Al local le llega un correo (`SMTP_*`) con todo para facturar. El comprador tiene un botón para avisar por WhatsApp.

Modos (`PAYMENTS_MODE`):
- `off`: solo WhatsApp. Es el actual.
- `payphone`: cobro real.
- `fake`: simulador, solo en localhost.

Probar con el simulador:
`SITE_URL=http://localhost:3000 PAYMENTS_MODE=fake docker compose --profile web up -d --build api web`

## 6. Dónde nos quedamos (2026-11-19)

**PayPhone: conexión OK**
- Causa del 401: la aplicación vieja era de **tipo API**, que no puede usar el Botón de Pagos. No se tocó.
- Con la cuenta de desarrollador que prestó el cliente se creó una **aplicación nueva tipo Web**:
  - plataforma .NET;
  - dominio `https://districauchosvch.com`;
  - respuesta `/pago/respuesta`.
  Su Token está en `.env` y **funciona**: la API crea el pedido y PayPhone devuelve el `payUrl`.
- **Store ID:** se deja **vacío** (`PAYPHONE_STORE_ID=`): los pagos van a la tienda principal. Probado OK el 2026-11-19 con la configuración real de `.env`. No es ningún campo de la aplicación de desarrollador (Identificador, Clave secreta, etc.); está en PayPhone Business, en Tiendas, y solo hace falta si el cliente quiere los pagos en una sucursal concreta.
- Abrir el `payUrl` fuera del dominio real (con curl desde local) muestra "No autorizado". Es lo esperado: PayPhone exige que el pago venga de `districauchosvch.com`. **La página de pago y el cobro de $1 se prueban en el servidor** (Semana 4).

**Cómo repetir la prueba de conexión** (no cobra nada):
1. `SITE_URL=https://districauchosvch.com PAYMENTS_MODE=payphone docker compose up -d api`
2. `POST /api/orders` con un producto de $1 → la respuesta debe traer `payUrl`.
3. Abrir el `payUrl`: debe decir "Pagar a Districauchos $1.00". **No pagar.**
4. Volver a la normalidad: `docker compose up -d api`. Borrar el pedido de prueba de la tabla `orders`.

**Repositorio**
- GitHub **privado**: `https://github.com/Pyaguari-cloud/districauchos-web`, rama `main`.
- Subido el 2026-11-19. El `git push` lo hace el usuario con `! git push`.

## 7. Pendientes

**Del cliente**
- [x] Token de una aplicación **Web** en PayPhone Developer (2026-11-19).
- [ ] Carpeta de fotos de Drive (el cliente ya la está preparando).
- [ ] Visto bueno y pago del servidor (ffxf Nano, ~$6 USD/mes).
- [ ] Tarjeta vigente en WordPress.com: el dominio se renueva el **10-dic-2026** ($13/año). **No cancelar** el plan Personal ($9/mes) hasta el lanzamiento.
- [ ] Los 18 productos del PDF de importación que en la hoja no figuran como VCH.
- [ ] Los 8 productos del Excel que no son VCH, y el precio de 9023781 ($13 por mayor contra $5 público).

**Nuestros: Semana 4**
- [ ] VPS **ffxf Nano** (1 vCPU, 2 GB, 20 GB; Montreal) + 2 GB de swap; las imágenes se construyen fuera del servidor, Docker, `.env` de producción y `secrets/google-sa.json`.
- [ ] Dominio: sigue registrado en WordPress.com; en su panel se cambian los nameservers a Cloudflare (gratis). Hoy el DNS solo tiene registros de WordPress, sin correo.
- [ ] Cloudflare: DNS, HTTPS y proxy hacia la web. La API **no se expone**: solo escucha en 127.0.0.1.
- [ ] SMTP para los avisos de pedidos.
- [ ] En el servidor, volver a correr `import-prices` con el Excel de sugeridos (ver README).
- [ ] `PAYMENTS_MODE=payphone` y una compra real de $1, que luego se anula desde PayPhone Business.
- [ ] Desactivar la aplicación PayPhone vieja cuando la nueva esté en producción.
- [ ] Revisar en el celular real antes del lanzamiento.

**Ideas para después**
- Medidas y fotos del PDF de importación. No se extrajeron porque el texto del PDF mezcla datos entre productos.
- Aviso automático por WhatsApp con la API oficial de Meta.
- Panel simple de pedidos para el local.

## 8. Cómo retomar en una terminal nueva

```bash
cd C:\dev\districauchos
docker compose --profile web up -d --build     # todo: db, search, worker, api, web
# web: http://localhost:3000  ·  API: http://localhost:8080/api/health
dotnet test                                    # pruebas
```

- Los archivos privados que no están en GitHub (`.env`, `secrets/`, `BRIEF-*.md`, `out/`) **solo existen en esta computadora**. Conviene tener una copia de seguridad aparte, por ejemplo en un gestor de contraseñas o un disco cifrado.
- Al terminar cada sesión de trabajo, actualizar §3, §6 y §7 de este archivo.
