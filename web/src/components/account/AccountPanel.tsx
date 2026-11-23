"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { accountChanged, cuenta } from "@/lib/account";
import { money } from "@/lib/format";
import { garage, useGarage, vehicleText } from "@/lib/vehicle";
import { FormError, PasswordField, SubmitButton, TextField } from "./Fields";

type Profile = { phone?: string | null; docType?: string | null; docNumber?: string | null; address?: string | null; city?: string | null };
type Order = { number: number; status: string; createdAt: string; total: number; delivery: string; items: number; summary: string };

const STATUS: Record<string, { label: string; cls: string }> = {
  Paid: { label: "Pagado", cls: "bg-ok-bg text-ok" },
  Review: { label: "En revisión", cls: "bg-ask-bg text-ask" },
  Canceled: { label: "Anulado", cls: "bg-gray-100 text-muted" },
};
const DELIVERY: Record<string, string> = {
  Pickup: "Retiro en local", Quito: "Envío en Quito", National: "Envío a provincia", Galapagos: "Envío a Galápagos",
};
const card = "flex flex-col gap-4 rounded-xl border border-line bg-white p-4 sm:p-6";
const h2 = "font-display text-2xl font-bold uppercase tracking-wide";
// "+593987654321" → "0987654321" para mostrarlo como lo escribe la gente.
const localPhone = (p?: string | null) => (p && p.startsWith("+5939") ? `0${p.slice(4)}` : p ?? "");

export function AccountPanel({ email, name, profile, orders }: { email: string; name: string; profile: Profile; orders: Order[] }) {
  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)] lg:items-start">
      <div className="flex flex-col gap-6">
        <ProfileForm email={email} name={name} profile={profile} />
        <Orders orders={orders} />
      </div>
      <div className="flex flex-col gap-6">
        <MyGarage />
        <PasswordForm />
        <Session />
        <DeleteAccount />
      </div>
    </div>
  );
}

function ProfileForm({ email, name, profile }: { email: string; name: string; profile: Profile }) {
  const router = useRouter();
  const [f, setF] = useState({
    name, phone: localPhone(profile.phone), docType: profile.docType ?? "cedula", docNumber: profile.docNumber ?? "",
    address: profile.address ?? "", city: profile.city ?? "",
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [msg, setMsg] = useState<{ ok: boolean; text: string }>();
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setF((s) => ({ ...s, [k]: e.target.value }));

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (busy) return;
    setBusy(true);
    setErrors({});
    setMsg(undefined);
    const r = await cuenta("perfil", "PUT", f);
    setBusy(false);
    if (r.ok) {
      setMsg({ ok: true, text: "Datos guardados." });
      accountChanged();
      router.refresh();
    } else {
      setErrors(r.data.fields ?? {});
      setMsg({ ok: false, text: r.data.error ?? "No pudimos guardar. Inténtalo de nuevo." });
    }
  }

  return (
    <form onSubmit={submit} noValidate className={card} aria-labelledby="mis-datos">
      <div>
        <h2 id="mis-datos" className={h2}>Mis datos</h2>
        <p className="text-muted">Se llenan solos cuando pagas en línea. Todos son opcionales menos el nombre.</p>
      </div>
      <TextField id="acc-email" label="Correo (tu usuario)" value={email} readOnly disabled />
      <TextField id="name" label="Nombre y apellido" autoComplete="name" value={f.name} onChange={set("name")} error={errors.name} required />
      <TextField id="phone" label="Celular" type="tel" autoComplete="tel" inputMode="tel" placeholder="0987654321" value={f.phone} onChange={set("phone")} error={errors.phone} />
      <div className="grid gap-4 sm:grid-cols-[10rem_minmax(0,1fr)]">
        <div className="flex flex-col gap-1">
          <label htmlFor="docType" className="font-semibold">Documento</label>
          <select id="docType" value={f.docType} onChange={set("docType")}
            className={`min-h-12 rounded-lg border bg-white px-3 text-base ${errors.docType ? "border-brand-red" : "border-line"}`}>
            <option value="cedula">Cédula</option>
            <option value="ruc">RUC</option>
            <option value="pasaporte">Pasaporte</option>
          </select>
        </div>
        <TextField id="docNumber" label="Número (para la factura)" inputMode={f.docType === "pasaporte" ? "text" : "numeric"} value={f.docNumber} onChange={set("docNumber")} error={errors.docNumber ?? errors.docType} />
      </div>
      <TextField id="address" label="Dirección de entrega" autoComplete="street-address" value={f.address} onChange={set("address")} error={errors.address} />
      <TextField id="city" label="Ciudad" autoComplete="address-level2" value={f.city} onChange={set("city")} error={errors.city} />
      {msg && (msg.ok
        ? <p role="status" className="rounded-lg bg-ok-bg px-3 py-2 text-ok">{msg.text}</p>
        : <FormError>{msg.text}</FormError>)}
      <SubmitButton busy={busy} busyText="Guardando…">Guardar datos</SubmitButton>
    </form>
  );
}

function Orders({ orders }: { orders: Order[] }) {
  return (
    <section className={card} aria-labelledby="mis-pedidos">
      <h2 id="mis-pedidos" className={h2}>Mis pedidos</h2>
      {orders.length === 0 ? (
        <p className="text-muted">
          Aquí verás los pedidos que pagues en línea mientras estés dentro de tu cuenta.{" "}
          <Link href="/buscar" className="font-semibold text-brand-ink underline underline-offset-4">Buscar repuestos</Link>
        </p>
      ) : (
        <ul className="flex flex-col divide-y divide-line">
          {orders.map((o) => {
            const st = STATUS[o.status] ?? { label: o.status, cls: "bg-gray-100 text-muted" };
            return (
              <li key={o.number} className="flex flex-col gap-1 py-3 first:pt-0 last:pb-0">
                <div className="flex flex-wrap items-baseline justify-between gap-2">
                  <p className="font-display text-lg font-bold">Pedido #{o.number}</p>
                  <p className="font-display text-lg font-bold">{money(o.total)}</p>
                </div>
                <p className="flex flex-wrap items-center gap-2 text-sm text-muted">
                  <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${st.cls}`}>{st.label}</span>
                  <span>{new Date(o.createdAt).toLocaleDateString("es-EC", { day: "numeric", month: "short", year: "numeric" })}</span>
                  <span>· {DELIVERY[o.delivery] ?? o.delivery}</span>
                  <span>· {o.items} {o.items === 1 ? "repuesto" : "repuestos"}</span>
                </p>
                {o.summary && <p className="text-sm">{o.summary}{o.items > 3 ? "…" : ""}</p>}
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}

function MyGarage() {
  const { vehicles } = useGarage();
  return (
    <section className={card} aria-labelledby="mi-garaje">
      <h2 id="mi-garaje" className={h2}>Mi garaje</h2>
      <p className="text-muted">Tus vehículos se guardan en tu cuenta: los verás en cualquier celular o computadora donde ingreses.</p>
      {vehicles.length > 0 && (
        <ul className="flex flex-col gap-1">
          {vehicles.map((v) => <li key={v.id} className="font-semibold">{vehicleText(v)}</li>)}
        </ul>
      )}
      <Link href="/mi-garaje" className="inline-flex min-h-11 items-center justify-center rounded-lg border-2 border-brand-black px-4 font-display font-bold uppercase tracking-wide hover:bg-brand-yellow">
        {vehicles.length ? "Administrar mi garaje" : "Agregar mi vehículo"}
      </Link>
    </section>
  );
}

function PasswordForm() {
  const [f, setF] = useState({ current: "", new: "" });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [msg, setMsg] = useState<{ ok: boolean; text: string }>();
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (busy) return;
    setBusy(true);
    setErrors({});
    setMsg(undefined);
    const r = await cuenta("contrasena", "PUT", f);
    setBusy(false);
    if (r.ok) {
      setF({ current: "", new: "" });
      setMsg({ ok: true, text: "Contraseña cambiada. Cerramos tu sesión en los demás equipos." });
    } else {
      setErrors(r.data.fields ?? {});
      setMsg({ ok: false, text: r.data.error ?? "No pudimos cambiar la contraseña." });
    }
  }

  return (
    <form onSubmit={submit} noValidate className={card} aria-labelledby="seguridad">
      <h2 id="seguridad" className={h2}>Contraseña</h2>
      <PasswordField id="current" label="Contraseña actual" autoComplete="current-password" value={f.current} onChange={(e) => setF((s) => ({ ...s, current: e.target.value }))} error={errors.current} />
      <PasswordField id="new" label="Contraseña nueva" autoComplete="new-password" value={f.new} onChange={(e) => setF((s) => ({ ...s, new: e.target.value }))} error={errors.new} hint="Al menos 8 caracteres." />
      {msg && (msg.ok ? <p role="status" className="rounded-lg bg-ok-bg px-3 py-2 text-ok">{msg.text}</p> : !Object.keys(errors).length && <FormError>{msg.text}</FormError>)}
      <SubmitButton busy={busy} busyText="Guardando…">Cambiar contraseña</SubmitButton>
    </form>
  );
}

function Session() {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  async function logout() {
    setBusy(true);
    await cuenta("salir", "POST", {});
    garage.clear(); // este equipo puede ser compartido; el garaje sigue guardado en la cuenta
    accountChanged();
    router.replace("/");
    router.refresh();
  }
  return (
    <section className={card} aria-labelledby="sesion">
      <h2 id="sesion" className={h2}>Sesión</h2>
      <p className="text-muted">Si usas un equipo compartido, cierra la sesión al terminar.</p>
      <button type="button" onClick={logout} disabled={busy}
        className="inline-flex min-h-12 items-center justify-center rounded-lg border-2 border-brand-black px-5 font-display text-lg font-bold uppercase tracking-wide hover:bg-brand-yellow disabled:opacity-60">
        {busy ? "Cerrando…" : "Cerrar sesión"}
      </button>
    </section>
  );
}

function DeleteAccount() {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (busy) return;
    setBusy(true);
    setError(undefined);
    const r = await cuenta("eliminar", "POST", { password });
    if (r.ok) {
      garage.clear();
      accountChanged();
      router.replace("/?cuenta=eliminada");
      router.refresh();
      return;
    }
    setError(r.data.fields?.password ?? r.data.error ?? "No pudimos eliminar la cuenta.");
    setBusy(false);
  }

  return (
    <section className={card} aria-labelledby="eliminar">
      <h2 id="eliminar" className={h2}>Eliminar cuenta</h2>
      {!open ? (
        <>
          <p className="text-muted">Borra tu cuenta, tus datos guardados y tu garaje. No se puede deshacer.</p>
          <button type="button" onClick={() => setOpen(true)} className="inline-flex min-h-11 items-center justify-center font-semibold text-brand-red underline underline-offset-4">
            Quiero eliminar mi cuenta
          </button>
        </>
      ) : (
        <form onSubmit={submit} noValidate className="flex flex-col gap-4">
          <p>
            Se borrarán tu cuenta, tus datos y tu garaje. <strong>No se puede deshacer.</strong> Los pedidos ya pagados se
            quedan en el registro del local porque los necesita para la factura, pero ya no estarán ligados a ti.
          </p>
          <PasswordField id="del-password" label="Escribe tu contraseña para confirmar" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} error={error} />
          <div className="flex flex-col gap-2 sm:flex-row">
            <button type="submit" disabled={busy || !password}
              className="inline-flex min-h-12 flex-1 items-center justify-center rounded-lg bg-brand-red px-5 font-display text-lg font-bold uppercase tracking-wide text-white disabled:opacity-60">
              {busy ? "Eliminando…" : "Eliminar mi cuenta"}
            </button>
            <button type="button" onClick={() => { setOpen(false); setPassword(""); setError(undefined); }}
              className="inline-flex min-h-12 flex-1 items-center justify-center rounded-lg border-2 border-brand-black px-5 font-display text-lg font-bold uppercase tracking-wide">
              Cancelar
            </button>
          </div>
        </form>
      )}
    </section>
  );
}
