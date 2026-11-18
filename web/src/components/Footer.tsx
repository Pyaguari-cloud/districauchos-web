import { contactLink } from "@/lib/whatsapp";
import { WhatsAppButton } from "./WhatsAppButton";

export function Footer() {
  const wa = contactLink();
  return (
    <footer className="mt-12 bg-brand-black text-white">
      <div className="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-8 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="font-display text-xl font-bold uppercase tracking-wide text-brand-yellow">Districauchos</p>
          <p className="text-sm text-gray-300">Todo en cauchos automotrices · Quito, Ecuador</p>
        </div>
        {wa && <WhatsAppButton href={wa} label="Escríbenos por WhatsApp" />}
      </div>
    </footer>
  );
}
