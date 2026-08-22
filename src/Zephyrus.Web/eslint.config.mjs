import nextCoreWebVitals from "eslint-config-next/core-web-vitals";
import nextTypeScript from "eslint-config-next/typescript";

/**
 * Flat config, the format ESLint 9 requires. eslint-config-next 16 exports
 * flat config arrays directly, so no compatibility shim is needed.
 */
const config = [
  {
    ignores: [".next/**", "next-env.d.ts"],
  },
  ...nextCoreWebVitals,
  ...nextTypeScript,
];

export default config;
