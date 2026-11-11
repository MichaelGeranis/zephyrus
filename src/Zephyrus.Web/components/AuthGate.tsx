"use client";

import { useEffect, useState } from "react";
import { getTeamToken } from "@/lib/auth";

/**
 * Every API call needs a team token. Without one, pages would load empty and
 * look as if there were no data, so show a sign-in prompt instead.
 */
export function AuthGate({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<"checking" | "signed-in" | "signed-out">("checking");

  useEffect(() => {
    Promise.resolve(getTeamToken()).then((token) => setState(token ? "signed-in" : "signed-out"));
  }, []);

  if (state === "checking") return null;
  if (state === "signed-in") return <>{children}</>;

  return (
    <div className="max-w-xl mx-auto mt-16 text-center space-y-3">
      <h1 className="text-2xl font-bold text-gray-900">Sign in to Zephyrus</h1>
      <p className="text-gray-600">
        Enter your team token in the box at the top right. Your administrator sets team tokens in
        the API configuration.
      </p>
      <p className="text-sm text-gray-500">
        Running the Docker demo? The demo tokens are listed in the README under “Run with Docker”.
      </p>
    </div>
  );
}
