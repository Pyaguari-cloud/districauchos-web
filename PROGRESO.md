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
2. **Nunca se muestran** costos ni en qué local está cada repuesto. **La cantidad total sí** (decisión del cliente 2026-11-19): suma de los locales y bodega con stock **positivo** (los negativos no restan: son datos sin cuadrar); si es 0, "Bajo pedido"/"Consultar".
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
| Pago en línea PayPhone | 🟢 **Activo en producción** (`PAYMENTS_MODE=payphone`) | Falta la compra real de $1 y anularla |
| Semana 4: servidor y lanzamiento | ✅ **En vivo desde el 2026-11-19** | VPS ffxf, Caddy, Cloudflare (proxy + Full strict), Brevo y PayPhone activo. Falta la compra real de $1 |

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

**Servidor (creado 2026-11-19)**
- ffxf, **cuenta propia del cliente** (pagada por él). `districauchos-1`: Nano, Montreal, Ubuntu 24.04, IPv4 `23.159.52.27`. Renueva el 19 de cada mes.
- Acceso: `ssh -i ~/.ssh/districauchos_ed25519 ubuntu@23.159.52.27` (usuario `ubuntu` con sudo; root no entra). La clave `districauchos_ed25519` es solo para este cliente y está solo en la computadora del desarrollador.
- Se creó con "solo clave": **no hay contraseña de consola VNC**. Hay que crearla si hace falta una entrada de emergencia.
- **Preparado (2026-11-19):**
  - sistema actualizado, zona horaria America/Guayaquil y actualizaciones de seguridad automáticas;
  - SSH solo con clave, sin root (`/etc/ssh/sshd_config.d/00-districauchos.conf`);
  - firewall ufw con 22, 80 y 443; fail2ban para sshd;
  - swap de 2 GB (swappiness 10);
  - Docker 29 + Compose, con registros limitados a 3×10 MB.
- **Ojo:** Docker publica sus puertos saltándose ufw. En producción, todo puerto de contenedor va en `127.0.0.1:` salvo el proxy web (80/443).
- **Página instalada (2026-11-19):**
  - responde en `http://23.159.52.27`, todavía sin dominio;
  - primera sincronización: 3.902 fichas (2.899 disponibles);
  - 65 precios por mayor del Excel cargados por túnel SSH;
  - pagos `off`;
  - los puertos internos (8080, 3000, 5433 y 7700) están cerrados desde internet.
- **Publicar cambios:** `./deploy.sh` (construye aquí, sube solo las imágenes que cambiaron y reinicia). Solo configuración: `./deploy.sh --files`. Exige que todo esté en commit.
- **`.env` del servidor:** copia del local con contraseñas **nuevas** (Postgres, Meilisearch y ADMIN_TOKEN, generadas en el servidor) más `COMPOSE_FILE`, `COMPOSE_PROFILES=web` y `SITE_ADDRESS=:80`. Para cambiar a HTTPS: `SITE_ADDRESS=districauchosvch.com, www.districauchosvch.com` y `docker compose up -d caddy`.
- Chrome fuerza HTTPS, así que por IP no se ve en el navegador (curl sí). Se verá al cambiar el dominio.
- Plan acordado: publicar **directo en districauchosvch.com** (sin subdominio de borrador). El cliente revisa en vivo y la página de WordPress.com queda un mes de respaldo.

**Cloudflare (2026-11-19)**
- Cuenta **del cliente**, plan Free, zona `districauchosvch.com`. Nameservers: `janet.ns.cloudflare.com` y `justin.ns.cloudflare.com`, ya guardados en WordPress.com. DNSSEC estaba apagado.
- Registros importados apuntan **todavía a WordPress.com** (`A` 192.0.78.184 y .236, `www` CNAME), en **DNS only**. La página vieja sigue en línea.
- **Lanzamiento:**
  1. en Cloudflare cambiar los `A` a `23.159.52.27` y `www` a CNAME del dominio, con el **proxy activado**;
  2. SSL/TLS en **Full (strict)**;
  3. en el servidor, `SITE_ADDRESS=districauchosvch.com, www.districauchosvch.com` y `docker compose up -d caddy`.

**Correo de avisos de pedidos (2026-11-19)**
- **Brevo**, cuenta del cliente, plan Free (300 correos al día). Dominio `districauchosvch.com` **autenticado**, con los registros brevo-code, DKIM `brevo1`/`brevo2` y DMARC con `rua` de Brevo en Cloudflare.
- Remitente `Districauchos VCH - Pedidos web <pedidos@districauchosvch.com>`, una dirección que solo envía (no tiene buzón). Las respuestas van al comprador por Reply-To.
- Destino: `ORDERS_EMAIL_TO=districauchosich@hotmail.com`.
- `SMTP_*` cargados en el `.env` del servidor (`smtp-relay.brevo.com:587`). La clave la generó el usuario y pasó al servidor sin mostrarse.
- Correo de prueba enviado OK el 2026-11-19. **El cliente confirmó que llegó a su Hotmail.**
- No se usó la conexión automática Brevo↔Cloudflare, para no dar permisos sobre el DNS.

**LANZAMIENTO (2026-11-19)**
- Cloudflare: `A @ → 23.159.52.27` **proxied**; se borró el `A` viejo de WordPress (192.0.78.184); `www` CNAME **proxied**. SSL/TLS en **Full (strict)**.
- Caddy con certificados Let's Encrypt para `districauchosvch.com` y `www` (vencen en feb-2027; se renuevan solos por HTTP-01 a través de Cloudflare). `www` → raíz (301), http → https.
- `PAYMENTS_MODE=payphone` en el servidor.
- **Volver a la página vieja** (emergencia): en Cloudflare, `A @ → 192.0.78.184` (y .236), DNS only. El plan WordPress.com sigue pagado hasta el 9-dic-2026.
- Producto de $1.00 para la prueba real: `/repuesto/4881722010-caucho-perno-estabilizador-toyota-celica-82`.

**Arreglos post-lanzamiento (2026-11-19)**
- Celular: el inicio se ensanchaba (efecto "zoom" + franja blanca) por códigos OEM y títulos largos sin espacios. Tarjetas con `min-w-0` + corte de palabras largas, categorías con el número debajo en celular, y `overflow-x: clip` en html/body. Verificado a 320, 360 y 390 px.
- Ícono de pestaña: era el de Next. Ahora `favicon.ico`, `icon.svg` y `apple-icon.png` con el auto "VCH" del logo.

**Observaciones del cliente tras el lanzamiento (2026-11-19)**
- "alza cabina dmax" salía vacío y en su POS no. Dos causas, **arregladas**: (1) "Mi garaje" tenía un Aveo y filtraba todo → ahora, si con el vehículo no hay nada, se muestran los de todos los vehículos con aviso; (2) el reintento quitaba palabras desde el final y se quedaba con "alza" → ahora prueba quitando una palabra cada vez (`ProductIndex.DropOneWord`). Resultado: las 3 "Base cabina DMAX" igual que el POS.
- **Productos sin OEM: publicados (aprobado por el cliente 2026-11-19).** Cada código es su propia ficha con clave `COD:{codigo}` (`CatalogKey`), URL `/repuesto/cod-{codigo}-…`, y la web muestra "Código" en vez de "OEM". El catálogo pasó de **3.902 a 8.429 fichas** (5.540 disponibles); las URLs de las fichas con OEM no cambiaron. Si el cliente llena el OEM de un producto, pasa solo a la ficha agrupada (su URL `cod-…` deja de existir).
- Buscador: tolera 1 letra de diferencia desde 4 letras (`alza` → `ALZAS`); los códigos con apóstrofo (`DH'40`) se buscan exacto.
- "Como Amazon" (guardar el carro): ya existe sin cuenta (localStorage, hasta 5). No se hará login. Posible mejora: hacer más visible que quedó guardado.
- Cantidades: se mantiene la regla de no mostrarlas (competencia; la hoja no es en tiempo real y hasta tiene existencias negativas). Opción a consultar: etiqueta "Últimas unidades" sin número.

**Nuevos pedidos del cliente (2026-11-19, "como Amazon"; la compra de prueba de $1 queda para el final)**
1. ✅ **Cantidades publicadas (2026-11-19).** Suma de los locales con stock positivo (se probó la suma bruta, pero dejaba 512 productos con unidades como "Bajo pedido" por negativos sin cuadrar, que promedian -62). Reporte para cuadrar: `out/negativos_para_cuadrar_<fecha>.csv`. "12 disponibles", "¡Solo quedan 2!" (≤ 3, `LOW_STOCK` en `Availability.tsx`). Si piden más de lo que hay: "3 disponibles + 2 bajo pedido" en carrito, pago, WhatsApp y correo. Sincronización cada **10 min** (`SYNC_INTERVAL_MINUTES=10` en el `.env` del servidor). **Revisión de stock al pagar ✅ (2026-11-19):** al pulsar "Pagar", la API deja el pedido en `stock_checks` y avisa por `NOTIFY districauchos_stock`; el Worker (`StockCheckService`) lee solo las 4 pestañas de stock en paralelo (~3–4 s), actualiza esos códigos en Postgres y en el buscador, y reutiliza una lectura de < 20 s (cuida la cuota de Google). Si el stock bajó o aparece "bajo pedido" sin aceptar → la API responde 409 y la web refresca el resumen. Si Google no responde en 8 s, se sigue con la última sync. La API sigue **sin** la llave de Google.

**Foto del vehículo en "Mi garaje" ✅ (2026-11-19):** pedido del cliente "como Amazon". 116 de 118 modelos con foto real de **Wikimedia Commons** (solo licencias libres CC BY / CC BY-SA / CC0 / dominio público; se descartó Google Imágenes por derechos de autor). Archivos en `web/public/vehiculos/*.webp` (640×400, ~4,4 MB en total) y créditos en `web/src/content/vehicle-photos.json`; el crédito sale bajo la foto en /buscar y completo en `/creditos` (enlace en el pie). Faltan Fiat Premio y Fiat Siena (Wikimedia respondió 429; reintentar). Las fotos que puso el usuario en `out/foto-vehiculos/` NO se usaron: eran de internet (un modelo a escala y una foto publicitaria de Fiat). Si el cliente manda fotos propias: `out/fotos-vehiculos/marca-modelo.jpg` (sin crédito). Fotos revisadas a mano: Corsa = Chevrolet Classic, QQ = 1.ª generación, Wingle = Wingle 7.

**Tiempos de entrega como Amazon ✅ (2026-11-19):** reglas del cliente: despacho el mismo día L–V hasta las 17:00 (hora de Ecuador); domingos y feriados no se despacha (el sábado sí); Quito llega el día del despacho (gratis); provincia por cooperativa o Servientrega en 24–48 h (+$5); **Galápagos** por avión ~3 días (+$80, `ENVIO_GALAPAGOS`); exterior solo por WhatsApp. Bajo pedido suma `BACKORDER_DAYS` días laborables. Lógica en `web/src/lib/eta.ts`; se ve en la ficha ("recíbelo hoy si pides en las próximas 2 h"), en cada opción del carrito y en el resumen del pago. **Feriados en `web/src/content/feriados.ts`: actualizar cada año con el decreto oficial** (2027 calculado por la regla de traslado, falta confirmarlo). Códigos genéricos (3, 3A, 7A…): el cliente decidió dejarlos (2026-11-19).

**Página "Mi garaje" ✅ (2026-11-20):** versión propia del "Tu garaje" de Amazon (revisado con el usuario): `/mi-garaje` con el vehículo activo en grande (foto + crédito), tarjetas de los demás (hasta 5), agregar con **año opcional**, quitar, y "Repuestos para tu Aveo" por categoría con cantidades (`/api/garaje`). La barra de arriba enlaza a la página. El año viaja en la cotización y en el pedido ("Chevrolet Aveo 2012"); hoy no filtra. Campo opcional **"Placa o VIN"** al pagar: va en el texto del vehículo del pedido para que el vendedor confirme (no se consulta ninguna base). **Descartado:** búsqueda por placa (datos de la ANT ligados al dueño, LOPDP) y decodificar VIN (no sirve para carros de Ecuador).

### Mejora "Buscar por año" — fases (definidas 2026-11-20)

Objetivo: el cliente pone su carro y año (ej. Aveo 2007) y ve los repuestos que le sirven; solo se ocultan los que sabemos que son de otros años.

| Fase | Qué | Estado |
|---|---|---|
| 1 | Mi garaje con año (hasta 5 carros) | ✅ publicado |
| 2 | Piloto de 50 OEM investigados (aprendizaje: fuentes de Ecuador, tablas, el vendedor debe verificar) | ✅ hecho · ⏳ falta que el vendedor revise `out/compatibilidades-piloto-50-FINAL.csv` |
| 3 | Años que ya están en las descripciones del inventario + filtro por año en el buscador | ✅ **publicado 2026-11-20**: 955 fichas con años (1.419 rangos), diccionario v2026-11-20 (+22 modelos y errores de tipeo). Probado en producción: "base" para Hilux = 19 sin año, 14 con 2008, 10 con 2020; Corolla 15 → 10 (2008) → 5 (2020) |
| 4 | Investigar los más vendidos sin año (tanda 2: 110 productos, otra IA) → reviso años → el vendedor revisa solo lo dudoso | 🔶 **esperando al vendedor**: años corregidos con las 31 tablas de Mansuera; piloto + tanda 2 = 158 productos, **92 con años**. Regla: se acepta solo si fuente de Ecuador/Colombia, confianza alta, el inventario menciona ese carro y sin contradicciones; se revisa solo lo dudoso **con años** (lo único que puede ocultar repuestos). Hoja para el vendedor: `out/REVISION-VENDEDOR-1.csv` (69 filas, 33 productos). Todo junto: `out/compatibilidades-TODO.csv` (scripts en el scratchpad: `oem/tanda2.py`, `oem/revision.py`) |
| 4b | **Tandas 3–5** (2026-11-20). Ya no quedan A/B sin años, así que son productos de clase C con más stock. **Tanda 3** revisada: `out/tanda-03-FINAL.csv` (182 filas; tablas de Mansuera leídas por mí; 4 OEM con la misma tabla genérica de Aveo y la bobina con "Creta 2008" van a revisión). Filas para agregar: `out/AGREGAR-AL-FINAL-tanda-03.csv` (172: 78 con Usar=Sí, 63 a revisar, 31 opcionales; nueva columna L "Tanda"). Simulación con las hojas reales: 215 filas aplicadas y 1.040 fichas con años. **Tandas 4 y 5** listas para la otra IA (`out/tanda-04…`, `out/tanda-05…`; quedan ~1.181 C). Hay 6 filas Sí que no se aplican porque el modelo no está en el diccionario (F-350, Verna, Optima, Opel, i30, Koleos): agregar modelos en la fase 6. El cliente revisa todas las tandas juntas cuando termine la importación **Tanda 3 en producción** (sync 124: 188 rangos de la hoja en 90 OEM; 1.040 fichas con años). **Tanda 4** revisada: `out/tanda-04-FINAL.csv` (151 filas; 19 tablas de Mansuera; el termostato Renault con 23 versiones y el H1 en texto van a revisión) → `out/AGREGAR-AL-FINAL-tanda-04.csv` (139: 75 Sí, 34 a revisar, 30 opcionales). Simulación: 289 filas aplicadas y 1.075 fichas con años. Filas Sí sin modelo en el diccionario: 15 (N200, Mazda 5, F-350, Verna, Optima, i30, Koleos, Focus, B1600, Sonic, S-Cross, Matiz, Opel) **Tanda 4 en producción** (243 rangos de la hoja en 125 OEM; 1.075 fichas con años). **Tanda 5** revisada: `out/tanda-05-FINAL.csv` (139 filas; 23 tablas de Mansuera; la tapa disco Kia con Rio R, Soul y Picanto va a revisión) → `out/AGREGAR-AL-FINAL-tanda-05.csv` (118: 70 Sí, 23 a revisar, 25 opcionales). Simulación: 356 filas aplicadas y **1.099** fichas con años. Filas Sí sin modelo en el diccionario: 23 → siguiente paso, agregar modelos (fase 6) **Tanda 5 en producción** (el usuario la subió). **Diccionario 2026-11-20b:** se agregaron 31 modelos (N200, Sonic, Matiz, FTR, NQR, NLR, Spin, Cobalt, Prisma, Joy, Super Carry y S-Cross de Suzuki, Verna, i30, Creta, Veloster, Optima, Soul, Seltos, Cadenza, Dyna/"dina", Echo, Mazda 5, B1600, F-350, Focus, Symbol, Koleos, Stepway, Saveiro, Damas). ISUZU pasa a ser alias de Chevrolet, y la marca de la hoja se resuelve con el diccionario. Equivalentes: Matiz (Chevrolet/Daewoo) y Super Carry (Chevrolet/Suzuki). Revisé ejemplos y no hay falsos positivos. Simulación: con carro 7.037 → 7.080; con años 1.099 → **1.127**; filas de la hoja aplicadas 356 → 378; solo queda sin reconocer "Opel Opel" | ✅ diccionario publicado (sync 127: 7.080 con carro, 1.127 con años) |
| 5 | Pestaña "Compatibilidades" en la hoja **principal** del cliente (la de "Codigos Existentes"), leída por el Worker (solo lectura) | ✅ **en producción** (2026-11-20). El usuario importó la pestaña en la hoja "Inventario" con permiso del cliente. Sync 111 ok: 136 rangos de la hoja en 66 OEM; las fichas con años pasan de 955 a **1.016**. Verificado en la ficha `/repuesto/96352644-distribuidor-agua-chevrolet-aveo`: Aveo, Chevy Taxi, Aveo Emotion y Aveo Family con motor y años. Queda pendiente que el vendedor llene "Usar" en las 69 filas con "¿Revisar?" = Sí. Columnas: OEM · Producto (solo referencia) · Marca · Modelo · Motor · Año desde · Año hasta · **Usar** (solo "Sí" cuenta) · ¿Revisar? · Por qué · Fuente. Pestaña opcional: si no existe, aviso `compat_tab_missing` y la sync sigue. La hoja agrega el carro si la descripción no lo decía y sus años (source `hoja`, con versión y motor para mostrar: "Aveo Family · 1.5"). El modelo de la hoja se lleva al del diccionario con el extractor; OEM sin ceros a la izquierda también empata (Sheets los quita). Archivo para pegar: `out/PARA-PEGAR-pestana-Compatibilidades.csv` (258 filas: 143 Usar=Sí automáticas, 69 ¿Revisar?=Sí para el vendedor, 46 opcionales sin años). Simulación con las hojas reales (`vehicles-report --compat-file`): 143 filas aplicadas, fichas con años 955 → 1.016. Se quitó la prueba local `COMPAT_FILE` / `web/src/lib/compat.ts`. **Falta:** publicar; el cliente crea la pestaña y pega el archivo; el vendedor llena "Usar" en las filas "¿Revisar? = Sí" (reemplaza a `REVISION-VENDEDOR-1.csv`) |
| 5b | **Mapa de locales** (pedido del cliente 2026-11-20, inspirado en "Agencias" de Mansuera; sin la franja de tutorial, que no la quiso). Leaflet + OpenStreetMap (sin clave ni cookies de Google) con un pin rojo por local; al tocarlo se ve dirección, teléfono y "Cómo llegar". Coordenadas sacadas de sus enlaces de Maps. La tarjeta muestra la foto de la fachada si `photo` está en `negocio.ts` | ✅ publicado (2026-11-20, 9673fc4). Las fotos son propias del cliente (dice que las tomó al abrir los locales; 408 px, se ven algo suaves). Están en `web/public/locales/`. Si manda fotos más grandes, se reemplazan con el mismo nombre |
| 6 | Mantenimiento: tandas de clase C si hay tiempo, catálogos de proveedores, pedir que las descripciones nuevas lleven años ("07/12") | ⏳ opcional · **Pedido del cliente (2026-11-20): verificar por placa como otra tienda.** Sigue descartado hacer la consulta nosotros (ANT/SRI con CAPTCHA, datos del dueño, LOPDP). Opciones: (a) proveedor de datos con contrato y licencia, solo si el cliente lo contrata y presenta su base legal; (b) reforzar lo actual: la placa o el VIN al pagar más la confirmación del vendedor antes de despachar, y un aviso "¿Seguro que le sirve?" cuando el carro del garaje no coincide. Es Mansuera (revisado 2026-11-20): tiene una pestaña "Por placa" en la portada, pero no dice de dónde salen los datos (su política de datos no menciona placas). El pie dice "Powered by Pardux", que puede ser su proveedor de software; no hay información pública. Ellos venden repuestos originales con catálogos de fábrica, así que la placa probablemente les da marca, modelo y año y eso lo cruzan con su catálogo; nosotros ya tenemos ese cruce (garaje + años). Decisión pendiente del cliente: contratar un proveedor de datos, o quedarse con garaje + placa/VIN al pagar |

**Siguiente: compatibilidades por OEM (aprobado por el cliente 2026-11-20).** Piloto de ~50 productos con OEM (el cliente no tiene lista de más vendidos → tomar Luv, Vitara, D-Max, Aveo, Corsa): investigar años/motor en catálogos con fuente, entregar hoja borrador (OEM, marca, modelo, año desde/hasta, motor, fuente, verificado); el cliente la sube a una hoja nueva de Google y su vendedor marca "Verificado"; la web la lee (solo lectura) y filtra por año solo lo verificado; lo no verificado se sigue mostrando con "confirma el año". 3.902 productos tienen OEM (2.901 con stock).

**Piloto OEM hecho (2026-11-20):** 50 productos clase A/B con stock (Aveo, Corsa, D-Max, Luv, Vitara; elegidos por `abc_class` porque el cliente no tiene lista de más vendidos). Resultado de la investigación en internet: **5 confianza alta, 15 media, 4 baja, 26 sin datos**. Los "sin datos" son sobre todo códigos de GM Brasil/región (Corsa 933…, Grand Vitara 938…) y códigos de proveedor que no son OEM. Los catálogos públicos son de EE.UU./Europa: los años no siempre coinciden con las versiones de Ecuador (Aveo Family/Activo, Corsa Evolution/Wind, Luv). Hoja para el vendedor: `out/compatibilidades-piloto-50.csv` (no se versiona). También salieron errores del inventario: OEM 9338368 (falta un dígito, el título dice 93383568), códigos con apóstrofes (8'97947'087'1, 09319'12047) y sufijos de proveedor (CHM, D, B, prefijo C). **Pendiente:** que el vendedor verifique; pedir a los proveedores (INR, A&G, MGT…) sus catálogos de aplicaciones, que cubren el mercado de la región mejor que internet; luego conectar la hoja verificada a la web (filtro por año solo para lo verificado). **Actualización 2026-11-20:** el usuario pidió una investigación más profunda a otra IA (`out/compatibilidades-piloto-50-investigado.csv`): encontró datos para los 50, casi todos de tiendas de Ecuador (Mansuera, Maxcar, catálogo Ecuamangueras). Sus enlaces eran reales, pero tomaba los años de la descripción automática y no de la tabla de compatibilidad; se corrigieron con las tablas de las 17 fichas de Mansuera. **Hoja final para el vendedor: `out/compatibilidades-piloto-50-FINAL.csv`**: una fila por vehículo (95 filas, 50 productos, 28 con años), con columna "Origen del dato". Ojo: Mansuera repite la misma tabla en varias mangueras del Aveo (posible tabla genérica), y la del 45202-65J00 es contradictoria. Es la data de otra tienda: sirve como referencia para verificar, no copiar sus textos ni fotos a la web. **Prueba en la web (2026-11-20, solo local):** `web/src/lib/compat.ts` lee el CSV de `COMPAT_FILE` (sin esa variable no hace nada: producción igual que antes); `compat-match.ts` cruza con el vehículo activo del garaje ("Aveo Family" cuenta como "Aveo", "Grand Vitara" NO como "Vitara"). La ficha muestra "✓ Le sirve a tu Corsa 2005" o "Es para otros años de tu D-Max" y una tabla "Compatibilidad por año" (* = por confirmar); las tarjetas del buscador muestran "✓ Sirve para tu Corsa 2005". Filas con Verificado = "No" se ignoran. Probar: `COMPAT_FILE=C:/dev/districauchos/out/compatibilidades-piloto-50-FINAL.csv API_URL=http://127.0.0.1:18080 npx next dev -p 3311` con el túnel SSH. **Falta para producción:** que el Worker lea la hoja de Google del cliente (pestaña Compatibilidades) a Postgres y la API la entregue en la ficha y el buscador (filtro por año en Meilisearch solo para lo verificado).

**Años desde las descripciones + filtro por año (2026-11-20, sin publicar todavía):** plan acordado con el usuario: no investigar todo (2.900 productos con OEM = ~60 revisiones), sino (1) leer los años que el cliente ya escribe en sus descripciones, (2) filtrar por año sin esconder lo que no tiene años, (3) investigar solo los más vendidos sin año (`out/tanda-02-mas-vendidos-PARA-INVESTIGAR.csv`, 110 productos, la otra IA). **Lectura:** `VehicleExtractor.ExtractYears` toma rangos "07/12", "03 - 11", "2004-2008", "(05/12)" y los asigna al modelo escrito antes ("Aveo/Rio 11/17" → ambos; "Hilux Fortuner 12-14 SR5 05-12" → los dos rangos); no toma años sueltos ni medidas (40X16X41, 40/16/41, 1.4-1.6). Resultado con el inventario real: **882 fichas con años** (reporte `vehicles-report` → `anios_detectados_*.csv`); los ~220 que se escapan son modelos que faltan en `data/vehicles.json` (4Runner, Hiace, Tercel, Twingo, Stylus…): agregarlos sube la cobertura. **Guardado:** tabla `product_vehicle_years` (source `descripcion`; la hoja de compatibilidades usará `hoja`). **Buscador:** campo `fit` en Meilisearch ("Chevrolet|Aveo|2007" por año conocido, "Chevrolet|Aveo|*" si no hay años); con `?anio=` el filtro es `fit IN [..|2007, ..|*]`: solo se ocultan los de otros años. **Web:** el año del garaje viaja en `anio` (buscador, barra Mi garaje, enlaces del garaje); la ficha y las tarjetas usan esos años (sin asterisco: los escribió el cliente).
2. **Tiempo de entrega** estilo Amazon. **Respuesta del cliente:** mismo día en Quito si se pide **hasta las 17:00, lunes a viernes**; fuera de Quito por **Servientrega** 24–48 h; **Galápagos 3 días por avión, igual que al exterior, con recargo de $80** por envío. **Domingos y feriados no se despacha**: lo pedido esos días sale el siguiente día laborable.
3. **Cuentas de usuario.** Propuesta: sin contraseñas (código al correo), compra como invitado se mantiene.
4. **Imagen del vehículo** elegido en "Mi garaje". Propuesta: siluetas propias por tipo de carrocería (las fotos de internet tienen derechos de autor).

**Repositorio**
- GitHub **privado**: `https://github.com/Pyaguari-cloud/districauchos-web`, rama `main`.
- Subido el 2026-11-19. El `git push` lo hace el usuario con `! git push`.

## 7. Pendientes

**Del cliente**
- [x] Token de una aplicación **Web** en PayPhone Developer (2026-11-19).
- [ ] Carpeta de fotos de Drive (el cliente ya la está preparando).
- [ ] Tarjeta vigente en WordPress.com: el dominio se renueva el **10-dic-2026** ($13/año). **No cancelar** el plan Personal ($9/mes) hasta el lanzamiento.
- [ ] Los 18 productos del PDF de importación que en la hoja no figuran como VCH.
- [ ] Los 8 productos del Excel que no son VCH, y el precio de 9023781 ($13 por mayor contra $5 público).

**Nuestros: Semana 4**
- [x] Servidor preparado: seguridad, swap y Docker (2026-11-19).
- [x] Página instalada en el VPS (2026-11-19). Era: VPS **ffxf Nano** (1 vCPU, 2 GB, 20 GB; Montreal) + 2 GB de swap; las imágenes se construyen fuera del servidor, Docker, `.env` de producción y `secrets/google-sa.json`.
- [x] Nameservers del dominio pasados a Cloudflare (2026-11-19). El dominio sigue registrado en WordPress.com.
- [x] Lanzamiento: DNS en Cloudflare apuntando al servidor, con proxy y Full (strict) (2026-11-19).
- [x] Cloudflare: DNS, HTTPS y proxy hacia la web (2026-11-19). La API no se expone.
- [x] SMTP para los avisos de pedidos (Brevo, 2026-11-19).
- [x] El cliente confirmó que recibe el correo de prueba en su Hotmail.
- [x] `import-prices` corrido contra la base del servidor (65 precios, 2026-11-19).
- [ ] `PAYMENTS_MODE=payphone` y una compra real de $1, que luego se anula desde PayPhone Business.
- [ ] Desactivar la aplicación PayPhone vieja (tipo API) después de la compra de prueba.
- [ ] Cancelar el plan WordPress.com Personal ($9/mes) **antes del 9-dic-2026**, sin tocar el dominio (que se renueva el 10-dic).
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
