"use client";

import { useState } from "react";

/** Campo de texto con etiqueta y error, igual que el formulario de pago (≥ 48 px, fuente 16 px en celular). */
export function TextField({
  id, label, error, hint, ...props
}: { id: string; label: string; error?: string; hint?: string } & React.InputHTMLAttributes<HTMLInputElement>) {
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="font-semibold">{label}</label>
      <input
        id={id} name={id}
        aria-invalid={!!error} aria-describedby={error ? `${id}-e` : hint ? `${id}-h` : undefined}
        className={`min-h-12 rounded-lg border bg-white px-3 text-base ${error ? "border-brand-red" : "border-line"}`}
        {...props}
      />
      {hint && !error && <p id={`${id}-h`} className="text-sm text-muted">{hint}</p>}
      {error && <p id={`${id}-e`} className="text-sm text-brand-red">{error}</p>}
    </div>
  );
}

/** Contraseña con botón "Mostrar" (ayuda mucho en celular). */
export function PasswordField({
  id, label, error, hint, ...props
}: { id: string; label: string; error?: string; hint?: string } & React.InputHTMLAttributes<HTMLInputElement>) {
  const [show, setShow] = useState(false);
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="font-semibold">{label}</label>
      <div className="relative">
        <input
          id={id} name={id} type={show ? "text" : "password"}
          aria-invalid={!!error} aria-describedby={error ? `${id}-e` : hint ? `${id}-h` : undefined}
          className={`min-h-12 w-full rounded-lg border bg-white px-3 pr-24 text-base ${error ? "border-brand-red" : "border-line"}`}
          {...props}
        />
        <button
          type="button" onClick={() => setShow((s) => !s)}
          className="absolute inset-y-0 right-0 min-w-20 rounded-r-lg px-3 text-sm font-semibold text-muted hover:text-brand-ink"
          aria-label={show ? "Ocultar contraseña" : "Mostrar contraseña"}
        >
          {show ? "Ocultar" : "Mostrar"}
        </button>
      </div>
      {hint && !error && <p id={`${id}-h`} className="text-sm text-muted">{hint}</p>}
      {error && <p id={`${id}-e`} className="text-sm text-brand-red">{error}</p>}
    </div>
  );
}

export function FormError({ children }: { children?: React.ReactNode }) {
  return children ? <p role="alert" className="rounded-lg bg-brand-red/10 px-3 py-2 text-brand-red">{children}</p> : null;
}

export function SubmitButton({ busy, children, busyText }: { busy: boolean; children: React.ReactNode; busyText: string }) {
  return (
    <button
      type="submit" disabled={busy}
      className="inline-flex min-h-12 w-full items-center justify-center rounded-lg bg-brand-yellow px-5 font-display text-lg font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark disabled:cursor-not-allowed disabled:opacity-60"
    >
      {busy ? busyText : children}
    </button>
  );
}

/** Solo rutas internas: evita que ?volver= mande a otra página. */
export const safeReturn = (v: string | null | undefined, fallback = "/cuenta") =>
  v && v.startsWith("/") && !v.startsWith("//") && !v.startsWith("/\\") ? v : fallback;
