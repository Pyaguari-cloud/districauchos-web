"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { accountChanged, cuenta, useAccountName } from "@/lib/account";
import { FormError, PasswordField, safeReturn, SubmitButton, TextField } from "./Fields";

const MIN = 8;

export function LoginForm() {
  const router = useRouter();
  const volver = safeReturn(useSearchParams().get("volver"));
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const name = useAccountName();

  // Llegó aquí con el nombre guardado pero sin sesión válida (venció o se cerró en otro equipo):
  // la consulta borra las cookies viejas y el encabezado vuelve a decir "Ingresar".
  useEffect(() => {
    if (!name) return;
    cuenta("yo", "GET").then((r) => {
      if (r.ok) router.replace(volver);
      else accountChanged();
    });
  }, [name, router, volver]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (busy) return;
    setBusy(true);
    setError(undefined);
    const r = await cuenta("ingresar", "POST", { email, password });
    if (r.ok) {
      accountChanged();
      router.replace(volver);
      router.refresh();
      return;
    }
    setError(r.data.error ?? "No pudimos ingresar. Inténtalo de nuevo.");
    setBusy(false);
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-4">
      <TextField id="email" label="Correo" type="email" autoComplete="email" inputMode="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
      <PasswordField id="password" label="Contraseña" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
      <FormError>{error}</FormError>
      <SubmitButton busy={busy} busyText="Ingresando…">Ingresar</SubmitButton>
      <div className="flex flex-col gap-2 text-center sm:flex-row sm:justify-between">
        <Link href="/cuenta/olvide" className="inline-flex min-h-11 items-center justify-center font-semibold underline underline-offset-4">¿Olvidaste tu contraseña?</Link>
        <Link href={`/cuenta/registro${volver !== "/cuenta" ? `?volver=${encodeURIComponent(volver)}` : ""}`} className="inline-flex min-h-11 items-center justify-center font-semibold underline underline-offset-4">Crear una cuenta</Link>
      </div>
    </form>
  );
}

export function RegisterForm() {
  const router = useRouter();
  const volver = safeReturn(useSearchParams().get("volver"));
  const [f, setF] = useState({ name: "", email: "", password: "" });
  const [accept, setAccept] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF((s) => ({ ...s, [k]: e.target.value }));

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (busy) return;
    const local: Record<string, string> = {};
    if (f.password.length < MIN) local.password = `La contraseña debe tener al menos ${MIN} caracteres.`;
    if (!accept) local.acceptPrivacy = "Para crear la cuenta acepta la política de privacidad.";
    if (Object.keys(local).length) { setErrors(local); setError("Revisa los datos marcados."); return; }
    setBusy(true);
    setErrors({});
    setError(undefined);
    const r = await cuenta("registro", "POST", { ...f, acceptPrivacy: accept });
    if (r.ok) {
      accountChanged();
      router.replace(volver);
      router.refresh();
      return;
    }
    setErrors(r.data.fields ?? {});
    setError(r.data.error ?? "No pudimos crear la cuenta. Inténtalo de nuevo.");
    setBusy(false);
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-4">
      <TextField id="name" label="Nombre y apellido" autoComplete="name" value={f.name} onChange={set("name")} error={errors.name} required />
      <TextField id="email" label="Correo" type="email" autoComplete="email" inputMode="email" value={f.email} onChange={set("email")} error={errors.email}
        hint="Será tu usuario para ingresar." required />
      <PasswordField id="password" label="Contraseña" autoComplete="new-password" value={f.password} onChange={set("password")} error={errors.password}
        hint={`Al menos ${MIN} caracteres. Mejor si mezclas letras y números.`} required minLength={MIN} />
      <div className="flex flex-col gap-1">
        <label className="flex cursor-pointer gap-3 text-sm">
          <input type="checkbox" checked={accept} onChange={(e) => setAccept(e.target.checked)} className="mt-0.5 h-5 w-5 shrink-0"
            aria-invalid={!!errors.acceptPrivacy} aria-describedby={errors.acceptPrivacy ? "accept-e" : undefined} />
          <span>
            Acepto la <Link href="/privacidad" target="_blank" className="font-semibold underline underline-offset-2">política de privacidad</Link> y
            que Districauchos guarde mis datos para mi cuenta y mis pedidos.
          </span>
        </label>
        {errors.acceptPrivacy && <p id="accept-e" className="text-sm text-brand-red">{errors.acceptPrivacy}</p>}
      </div>
      <FormError>{error}</FormError>
      <SubmitButton busy={busy} busyText="Creando tu cuenta…">Crear cuenta</SubmitButton>
      <p className="text-center">
        ¿Ya tienes cuenta?{" "}
        <Link href={`/cuenta/ingresar${volver !== "/cuenta" ? `?volver=${encodeURIComponent(volver)}` : ""}`} className="inline-flex min-h-11 items-center font-semibold underline underline-offset-4">Ingresar</Link>
      </p>
    </form>
  );
}

export function ForgotForm() {
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string>();
  const [sent, setSent] = useState(false);
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (busy) return;
    setBusy(true);
    setError(undefined);
    const r = await cuenta("olvide", "POST", { email });
    setBusy(false);
    if (r.ok) setSent(true);
    else setError(r.data.fields?.email ?? r.data.error ?? "No pudimos enviar el correo. Inténtalo de nuevo.");
  }

  if (sent) {
    return (
      <div className="flex flex-col gap-3 rounded-lg bg-ok-bg p-4 text-ok" role="status">
        <p className="font-semibold">Revisa tu correo.</p>
        <p>
          Si <strong>{email}</strong> tiene una cuenta, te llegará un enlace para elegir una contraseña nueva. Vale 30 minutos.
          Si no lo ves, revisa la carpeta de correo no deseado.
        </p>
      </div>
    );
  }
  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-4">
      <TextField id="email" label="Correo de tu cuenta" type="email" autoComplete="email" inputMode="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
      <FormError>{error}</FormError>
      <SubmitButton busy={busy} busyText="Enviando…">Enviarme el enlace</SubmitButton>
      <Link href="/cuenta/ingresar" className="inline-flex min-h-11 items-center justify-center font-semibold underline underline-offset-4">Volver a ingresar</Link>
    </form>
  );
}

export function ResetForm() {
  // El token llega después de # (no viaja al servidor ni queda en registros).
  const [token, setToken] = useState<string | null | undefined>(undefined);
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string>();
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const t = new URLSearchParams(window.location.hash.slice(1)).get("t");
    // eslint-disable-next-line react-hooks/set-state-in-effect -- el # solo se puede leer en el navegador
    setToken(t && /^[A-Za-z0-9_-]{40,64}$/.test(t) ? t : null);
    if (t) history.replaceState(null, "", window.location.pathname); // que no quede en el historial
  }, []);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (busy || !token) return;
    if (password.length < MIN) { setError(`La contraseña debe tener al menos ${MIN} caracteres.`); return; }
    setBusy(true);
    setError(undefined);
    const r = await cuenta("restablecer", "POST", { token, password });
    setBusy(false);
    if (r.ok) setDone(true);
    else setError(r.data.fields?.password ?? r.data.error ?? "No pudimos cambiar la contraseña.");
  }

  if (token === undefined) return null;
  if (done) {
    return (
      <div className="flex flex-col gap-3">
        <p className="rounded-lg bg-ok-bg p-4 font-semibold text-ok" role="status">Listo, tu contraseña cambió. Ya puedes ingresar con la nueva.</p>
        <Link href="/cuenta/ingresar" className="inline-flex min-h-12 items-center justify-center rounded-lg bg-brand-yellow px-5 font-display text-lg font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark">Ingresar</Link>
      </div>
    );
  }
  if (!token) {
    return (
      <div className="flex flex-col gap-3">
        <FormError>El enlace no es válido o está incompleto. Pide uno nuevo.</FormError>
        <Link href="/cuenta/olvide" className="inline-flex min-h-11 items-center justify-center font-semibold underline underline-offset-4">Pedir un enlace nuevo</Link>
      </div>
    );
  }
  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-4">
      <PasswordField id="password" label="Contraseña nueva" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)}
        hint={`Al menos ${MIN} caracteres.`} required minLength={MIN} />
      <FormError>{error}</FormError>
      <SubmitButton busy={busy} busyText="Guardando…">Guardar contraseña</SubmitButton>
    </form>
  );
}
