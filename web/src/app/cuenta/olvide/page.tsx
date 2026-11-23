import type { Metadata } from "next";
import { AuthCard } from "@/components/account/AuthCard";
import { ForgotForm } from "@/components/account/AuthForms";

export const metadata: Metadata = { title: "Recuperar contraseña", robots: { index: false } };

export default function ForgotPage() {
  return (
    <AuthCard title="¿Olvidaste tu contraseña?" intro="Escribe el correo de tu cuenta y te enviamos un enlace para elegir una nueva.">
      <ForgotForm />
    </AuthCard>
  );
}
