"use client";

export default function ErrorPage({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <div className="mx-auto flex max-w-xl flex-col items-start gap-4 py-10">
      <h1 className="font-display text-3xl font-bold uppercase">Algo salió mal</h1>
      <p className="text-muted">No pudimos cargar el catálogo en este momento. Intenta de nuevo en unos segundos.</p>
      <button type="button" onClick={reset} className="min-h-11 rounded-lg bg-brand-yellow px-5 font-display font-bold uppercase text-black">
        Reintentar
      </button>
    </div>
  );
}
