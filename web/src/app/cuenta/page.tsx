import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { AccountPanel } from "@/components/account/AccountPanel";
import { getMe, getMyOrders } from "@/lib/session";

export const metadata: Metadata = { title: "Mi cuenta", robots: { index: false } };

export default async function AccountPage() {
  const me = await getMe();
  if (!me) redirect("/cuenta/ingresar");
  const orders = await getMyOrders();
  const first = me.name.split(" ")[0];
  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="font-display text-3xl font-bold uppercase tracking-wide">Hola, {first}</h1>
        <p className="text-muted">Tu cuenta de Districauchos: tus datos, tu garaje y tus pedidos.</p>
      </div>
      <AccountPanel email={me.email} name={me.name} profile={me.profile} orders={orders} />
    </div>
  );
}
