# web (Next.js) — semana 2

Pendiente. Se crea con:

```bash
npx create-next-app@latest . --ts --tailwind --app --eslint --src-dir --import-alias "@/*"
```

Rutas previstas (brief §9): `/`, `/buscar`, `/repuesto/[slug]`, `/carrito`.
Necesita un `Dockerfile` (output `standalone`) para activar el servicio `web` de `docker-compose.yml` (`--profile web`).
