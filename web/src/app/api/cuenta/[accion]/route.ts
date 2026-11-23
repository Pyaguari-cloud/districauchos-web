import { NextResponse } from "next/server";
import { accountApi, clearSession, sameOrigin, sessionToken, setName, setSession } from "@/lib/session";

/**
 * Puente de las cuentas de clientes hacia la API .NET. Aquí se ponen y quitan las cookies de sesión;
 * la validación de verdad (contraseñas, intentos, datos) la hace la API.
 */
type Ctx = { params: Promise<{ accion: string }> };
const MAX_BODY = 8 * 1024;
const noStore = { "Cache-Control": "no-store" };

const str = (v: unknown, max: number) => (typeof v === "string" ? v.slice(0, max) : undefined);

async function readBody(req: Request): Promise<Record<string, unknown> | null> {
  const text = await req.text();
  if (text.length > MAX_BODY) return null;
  try {
    const v = JSON.parse(text || "{}");
    return v && typeof v === "object" ? (v as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

/** Respuesta de la API tal cual (error y campos marcados), o un error genérico si la API no responde. */
async function relay(res: Response, extra?: (data: Record<string, unknown>, out: NextResponse) => void) {
  const raw = (await res.json().catch(() => ({}))) as Record<string, unknown> | unknown[];
  if (res.ok && Array.isArray(raw)) return NextResponse.json(raw, { headers: noStore }); // garaje, pedidos
  const data = Array.isArray(raw) ? {} : raw;
  if (!res.ok) {
    const out = NextResponse.json(
      { error: typeof data.error === "string" ? data.error : "No pudimos completar la acción. Inténtalo de nuevo.", fields: data.fields ?? {} },
      { status: res.status, headers: noStore },
    );
    if (res.status === 401 && !("fields" in data)) clearSession(out); // sesión vencida o cerrada en otro equipo
    return out;
  }
  // El token va solo en la cookie httpOnly: nunca en el cuerpo que recibe el navegador.
  const { token, ...rest } = data;
  const out = NextResponse.json(rest, { headers: noStore });
  extra?.({ ...rest, token }, out);
  return out;
}

const offline = () => NextResponse.json({ error: "El servicio no está disponible en este momento. Inténtalo en unos minutos." }, { status: 502, headers: noStore });

export async function GET(_req: Request, { params }: Ctx) {
  const { accion } = await params;
  if (!(await sessionToken())) return NextResponse.json({ error: "Ingresa a tu cuenta." }, { status: 401, headers: noStore });
  try {
    if (accion === "yo") return relay(await accountApi("/me"));
    if (accion === "garaje") {
      const res = await accountApi("/me");
      if (!res.ok) return relay(res);
      const me = (await res.json()) as { garage?: unknown[] };
      return NextResponse.json(me.garage ?? [], { headers: noStore });
    }
    if (accion === "pedidos") return relay(await accountApi("/orders"));
  } catch {
    return offline();
  }
  return NextResponse.json({ error: "No encontrado" }, { status: 404 });
}

export async function POST(req: Request, ctx: Ctx) {
  return mutate(req, ctx, "POST");
}

export async function PUT(req: Request, ctx: Ctx) {
  return mutate(req, ctx, "PUT");
}

async function mutate(req: Request, { params }: Ctx, method: "POST" | "PUT") {
  const { accion } = await params;
  if (!sameOrigin(req)) return NextResponse.json({ error: "Pedido no permitido" }, { status: 403 });

  // El garaje se manda como lista; el resto como objeto.
  if (method === "PUT" && accion === "garaje") {
    const text = await req.text();
    if (text.length > MAX_BODY) return NextResponse.json({ error: "Demasiados datos" }, { status: 413 });
    let list: unknown;
    try { list = JSON.parse(text); } catch { return NextResponse.json({ error: "Datos inválidos" }, { status: 400 }); }
    if (!Array.isArray(list)) return NextResponse.json({ error: "Datos inválidos" }, { status: 400 });
    try { return relay(await accountApi("/garage", { method: "PUT", body: list.slice(0, 5), req })); } catch { return offline(); }
  }

  const b = await readBody(req);
  if (!b) return NextResponse.json({ error: "Datos inválidos" }, { status: 400 });
  const login = (data: Record<string, unknown>, out: NextResponse) => {
    if (typeof data.token === "string") setSession(out, data.token, String(data.name ?? ""));
  };

  try {
    switch (`${method} ${accion}`) {
      case "POST ingresar":
        return relay(await accountApi("/login", { method: "POST", body: { email: str(b.email, 120), password: str(b.password, 200) }, req, token: null }), login);
      case "POST registro":
        return relay(await accountApi("/register", {
          method: "POST", req, token: null,
          body: { email: str(b.email, 120), name: str(b.name, 100), password: str(b.password, 200), acceptPrivacy: b.acceptPrivacy === true },
        }), login);
      case "POST salir": {
        await accountApi("/logout", { method: "POST", req }).catch(() => null);
        const out = NextResponse.json({ ok: true }, { headers: noStore });
        clearSession(out);
        return out;
      }
      case "PUT perfil":
        return relay(await accountApi("/profile", {
          method: "PUT", req,
          body: {
            name: str(b.name, 100), phone: str(b.phone, 20), docType: str(b.docType, 20), docNumber: str(b.docNumber, 20),
            address: str(b.address, 200), city: str(b.city, 60),
          },
        }), (data, out) => setName(out, String(data.name ?? "")));
      case "PUT contrasena":
        return relay(await accountApi("/password", { method: "PUT", req, body: { current: str(b.current, 200), new: str(b.new, 200) } }), login);
      case "POST olvide":
        return relay(await accountApi("/password/forgot", { method: "POST", req, token: null, body: { email: str(b.email, 120) } }));
      case "POST restablecer":
        return relay(await accountApi("/password/reset", { method: "POST", req, token: null, body: { token: str(b.token, 64), password: str(b.password, 200) } }));
      case "POST eliminar": {
        const res = await accountApi("/delete", { method: "POST", req, body: { password: str(b.password, 200) } });
        if (!res.ok) return relay(res);
        const out = NextResponse.json({ ok: true }, { headers: noStore });
        clearSession(out);
        return out;
      }
    }
  } catch {
    return offline();
  }
  return NextResponse.json({ error: "No encontrado" }, { status: 404 });
}
