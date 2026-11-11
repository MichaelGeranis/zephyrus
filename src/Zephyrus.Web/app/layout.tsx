import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { SessionBar } from "@/components/SessionBar";
import { AuthGate } from "@/components/AuthGate";
import "./globals.css";

export const metadata: Metadata = {
  title: "Zephyrus",
  description: "AI-powered software delivery platform",
};

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  // Render per request: the Content-Security-Policy nonce (proxy.ts) is new
  // for every response, and a page built ahead of time would not carry it.
  await connection();

  return (
    <html lang="en">
      <body className="min-h-screen bg-gray-50">
        <header className="bg-white border-b border-gray-200">
          <div className="max-w-5xl mx-auto px-4 py-3 flex items-center gap-6">
            <Link href="/projects" className="text-lg font-bold text-gray-900">
              Zephyrus
            </Link>
            <nav className="flex gap-4 text-sm text-gray-600">
              <Link href="/dashboard" className="hover:text-gray-900">
                Dashboard
              </Link>
              <Link href="/projects" className="hover:text-gray-900">
                Projects
              </Link>
            </nav>
            <SessionBar />
          </div>
        </header>
        <main className="max-w-5xl mx-auto px-4 py-8">
          <AuthGate>{children}</AuthGate>
        </main>
      </body>
    </html>
  );
}
