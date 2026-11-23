import type { Metadata } from "next";
import { Suspense } from "react";
import { AuthCard } from "@/components/account/AuthCard";
import { RegisterForm } from "@/components/account/AuthForms";

export const metadata: Metadata = { title: "Crear cuenta", robots: { index: false } };

export default function RegisterPage() {
  return (
    <AuthCard
      title="Crear cuenta"
      intro="Gratis y opcional. Guarda tus vehículos y tus datos para pagar más rápido, y revisa tus pedidos cuando quieras."
    >
      <Suspense><RegisterForm /></Suspense>
    </AuthCard>
  );
}
