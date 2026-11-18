import Link from "next/link";
import { SearchBox } from "@/components/SearchBox";

export default function NotFound() {
  return (
    <div className="mx-auto flex max-w-xl flex-col gap-4 py-10">
      <h1 className="font-display text-3xl font-bold uppercase">No encontramos esa página</h1>
      <p className="text-muted">Puede que el repuesto haya cambiado de código. Búscalo de nuevo:</p>
      <div className="rounded-xl bg-brand-ink p-4"><SearchBox size="lg" /></div>
      <Link href="/" className="font-semibold underline underline-offset-4">Ir al inicio</Link>
    </div>
  );
}
