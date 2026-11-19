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
| Pago en línea PayPhone | 🟡 Programado y probado con el simulador | **Falta un Token válido** (ver §6) |
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

**Envíos**
- Quito gratis.
- Provincia +$5 por cooperativa.
- Internacional: se consulta.

**Pagos**
- **PayPhone directo en la página**: el comprador paga como invitado, sin cuenta.
- También **transferencia** al Banco Pichincha (los datos están en `web/src/content/negocio.ts`).

**Sin stock**
- Se puede pagar como **"Bajo pedido, llega en ~4 días"**, y el comprador lo acepta antes de pagar (`BACKORDER_DAYS=4`).
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

**PayPhone**
- El Token y el Store ID que dio el cliente están en `.env`.
- La prueba de conexión (pedir un enlace de $1, sin cobrar) dio **401: "Su aplicación no está autorizada…"**.
- El Token está bien copiado. El problema es la aplicación de donde salió: probablemente no es de **tipo Web**, se copió otro campo, o es la aplicación vieja del programador anterior.
- **Solución:** crear en developer.payphone.app una **aplicación nueva tipo Web** con:
  - dominio `https://districauchosvch.com`;
  - URL de respuesta `https://districauchosvch.com/pago/respuesta`.
  Después se copia su Token al `.env` y se repite la prueba.
- El cliente invitó al desarrollador como "developer", pero la invitación **no deja entrar**: no llega correo de recuperación y la cuenta parece no estar activada.
- **Plan B:** una videollamada en la que el cliente crea la aplicación desde su cuenta.

**Cómo repetir la prueba de conexión** (no cobra nada):
1. `SITE_URL=https://districauchosvch.com PAYMENTS_MODE=payphone docker compose up -d api`
2. `POST /api/orders` con un producto de $1 → la respuesta debe traer `payUrl`.
3. Abrir el `payUrl`: debe decir "Pagar a Districauchos $1.00". **No pagar.**
4. Volver a la normalidad: `docker compose up -d api`. Borrar el pedido de prueba de la tabla `orders`.

**Repositorio**
- Se está subiendo a GitHub, en un repositorio **privado**.

## 7. Pendientes

**Del cliente**
- [ ] Token de una aplicación **Web** en PayPhone Developer, o activar la invitación de desarrollador.
- [ ] Captura de cómo les llega hoy el aviso de venta al WhatsApp: ¿lo manda su tienda o la app PayPhone?
- [ ] Confirmar "bajo pedido ~4 días".
- [ ] Correo para los avisos de pedidos. Por defecto: districauchosich@hotmail.com.
- [ ] Credenciales del dominio y la carpeta de fotos de Drive.
- [ ] Los 18 productos del PDF de importación que en la hoja no figuran como VCH.
- [ ] Los 8 productos del Excel que no son VCH, y el precio de 9023781 ($13 por mayor contra $5 público).

**Nuestros: Semana 4**
- [ ] VPS, Docker, `.env` de producción y `secrets/google-sa.json`.
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
