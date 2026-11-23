import type { Metadata } from "next";
import { AuthCard } from "@/components/account/AuthCard";
import { ResetForm } from "@/components/account/AuthForms";

export const metadata: Metadata = { title: "Nueva contraseña", robots: { index: false }, referrer: "no-referrer" };

export default function ResetPage() {
  return (
    <AuthCard title="Nueva contraseña" intro="Elige tu contraseña nueva. Al guardarla se cierra la sesión en todos tus equipos.">
      <ResetForm />
    </AuthCard>
  );
}
