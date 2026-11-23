/** Marco angosto de las páginas de ingresar / registro / recuperar. */
export function AuthCard({ title, intro, children }: { title: string; intro?: React.ReactNode; children: React.ReactNode }) {
  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-4">
      <div>
        <h1 className="font-display text-3xl font-bold uppercase tracking-wide">{title}</h1>
        {intro && <p className="mt-1 text-muted">{intro}</p>}
      </div>
      <div className="rounded-xl border border-line bg-white p-4 sm:p-6">{children}</div>
    </div>
  );
}
