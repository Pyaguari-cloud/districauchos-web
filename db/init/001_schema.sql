-- Districauchos catálogo · Etapa 1 · esquema inicial (brief §7)
-- Todas las tablas salvo price_overrides y sync_runs se reconstruyen en cada sincronización.
-- "precio de COSTO" NO se guarda en ninguna tabla.

CREATE TABLE IF NOT EXISTS products_raw (
    code         text        NOT NULL,
    code_norm    text        NOT NULL,
    description  text,
    pvp          numeric(12,2),
    category     text,
    subcategory  text,
    oem          text,
    oem_norm     text,
    supplier     text,
    alt_code     text,
    abc_class    text,
    source_row   int         NOT NULL,
    synced_at    timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_products_raw_code_norm ON products_raw (code_norm);
CREATE INDEX IF NOT EXISTS ix_products_raw_oem_norm  ON products_raw (oem_norm);

-- Interna: cantidades y ubicación nunca se exponen en la API pública.
CREATE TABLE IF NOT EXISTS stock_raw (
    code_norm   text          NOT NULL,
    location    text          NOT NULL CHECK (location IN ('tabiazo','cusubamba','maldonado','bodega')),
    stock       numeric(12,2) NOT NULL,
    synced_at   timestamptz   NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_stock_raw_code_norm ON stock_raw (code_norm);

CREATE TABLE IF NOT EXISTS catalog_items (
    oem_norm     text PRIMARY KEY,
    slug         text          NOT NULL UNIQUE,
    title        text          NOT NULL,
    description  text          NOT NULL,
    category     text,
    subcategory  text,
    is_vch       boolean       NOT NULL,
    pvp_min      numeric(12,2) NOT NULL,
    pvp_max      numeric(12,2) NOT NULL,
    mayor_min    numeric(12,2),
    available    boolean       NOT NULL,
    stock        numeric(12,2) NOT NULL DEFAULT 0,
    abc_class    text,
    oem_aliases  text[]        NOT NULL DEFAULT '{}',
    codes        text[]        NOT NULL DEFAULT '{}',
    alt_codes    text[]        NOT NULL DEFAULT '{}',
    image_path   text,
    updated_at   timestamptz   NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_catalog_items_category ON catalog_items (category, subcategory);

CREATE TABLE IF NOT EXISTS catalog_variants (
    oem_norm      text          NOT NULL REFERENCES catalog_items (oem_norm) ON DELETE CASCADE,
    code          text          NOT NULL,
    supplier      text,
    is_vch        boolean       NOT NULL,
    pvp           numeric(12,2) NOT NULL,
    precio_mayor  numeric(12,2),
    available     boolean       NOT NULL,
    stock         numeric(12,2) NOT NULL DEFAULT 0,
    PRIMARY KEY (oem_norm, code)
);

-- model = '' cuando solo se detectó la marca.
CREATE TABLE IF NOT EXISTS product_vehicles (
    oem_norm  text NOT NULL REFERENCES catalog_items (oem_norm) ON DELETE CASCADE,
    brand     text NOT NULL,
    model     text NOT NULL DEFAULT '',
    PRIMARY KEY (oem_norm, brand, model)
);
CREATE INDEX IF NOT EXISTS ix_product_vehicles_brand_model ON product_vehicles (brand, model);

CREATE TABLE IF NOT EXISTS product_vehicle_years (
    oem_norm  text NOT NULL REFERENCES catalog_items (oem_norm) ON DELETE CASCADE,
    brand     text NOT NULL,
    model     text NOT NULL,
    year_from int  NOT NULL,
    year_to   int  NOT NULL,
    source    text NOT NULL,
    version   text NOT NULL DEFAULT '',
    engine    text NOT NULL DEFAULT ''
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_product_vehicle_years
    ON product_vehicle_years (oem_norm, brand, model, version, engine, year_from, year_to, source);

-- Cilindraje por vehículo (de la descripción o de la hoja de compatibilidades).
CREATE TABLE IF NOT EXISTS product_vehicle_engines (
    oem_norm text NOT NULL REFERENCES catalog_items (oem_norm) ON DELETE CASCADE,
    brand    text NOT NULL,
    model    text NOT NULL,
    engine   text NOT NULL,
    source   text NOT NULL,
    PRIMARY KEY (oem_norm, brand, model, engine)
);

-- Foto elegida a mano para una categoría del inicio (archivo CAT-{categoría} en Drive o media/productos).
CREATE TABLE IF NOT EXISTS category_images (
    category text PRIMARY KEY,
    image    text NOT NULL
);

-- Mantenida a mano / importada del Excel de precios sugeridos (pregunta 12.1). La sync no la borra.
CREATE TABLE IF NOT EXISTS price_overrides (
    oem_norm      text PRIMARY KEY,
    precio_mayor  numeric(12,2),
    precio_menor  numeric(12,2),
    source        text NOT NULL
);

CREATE TABLE IF NOT EXISTS sync_runs (
    id           bigserial PRIMARY KEY,
    started_at   timestamptz NOT NULL,
    finished_at  timestamptz,
    status       text        NOT NULL CHECK (status IN ('running','ok','failed')),
    rows_read    jsonb       NOT NULL DEFAULT '{}',
    published    int,
    warnings     jsonb       NOT NULL DEFAULT '[]',
    error        text
);
CREATE INDEX IF NOT EXISTS ix_sync_runs_started ON sync_runs (started_at DESC);
