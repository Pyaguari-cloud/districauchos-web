import type { Metadata } from "next";
import { Suspense } from "react";
import { AuthCard } from "@/components/account/AuthCard";
import { LoginForm } from "@/components/account/AuthForms";

export const metadata: Metadata = { title: "Ingresar", robots: { index: false } };

export default function LoginPage() {
  return (
    <AuthCard title="Ingresar" intro="Con tu cuenta guardas tu garaje, tus datos para pagar y ves tus pedidos. Puedes comprar sin cuenta.">
      <Suspense><LoginForm /></Suspense>
    </AuthCard>
  );
}
