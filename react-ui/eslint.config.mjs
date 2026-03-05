import { defineConfig, globalIgnores } from 'eslint/config';
import nextVitals from 'eslint-config-next/core-web-vitals';
import nextTs from 'eslint-config-next/typescript';
import prettierConfig from 'eslint-config-prettier';

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  prettierConfig,
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    '.next/**',
    'out/**',
    'build/**',
    'next-env.d.ts',
    // Generated files - never edit manually
    'types/generated/**',
  ]),
  {
    rules: {
      // TypeScript inference handles return types well; explicit types add noise
      // without catching real bugs in a React codebase.
      '@typescript-eslint/explicit-function-return-type': 'off',
      // Disallow any type (use unknown instead)
      '@typescript-eslint/no-explicit-any': 'error',
      // Prefer const over let when not reassigned
      'prefer-const': 'error',
      // No unused variables (prefix with _ if intentionally unused)
      '@typescript-eslint/no-unused-vars': [
        'error',
        {
          argsIgnorePattern: '^_',
          varsIgnorePattern: '^_',
        },
      ],
      // Console logging is allowed during development.
      'no-console': 'off',
      // All <img> usages are player avatars from the game server with dynamic
      // URLs (localhost, LAN, Azure). next/image optimization doesn't apply.
      '@next/next/no-img-element': 'off',
      // React Compiler rules: downgrade to warn for pre-existing patterns.
      // setState-in-effect and ref-access-during-render are common React
      // patterns that work correctly but violate strict compiler rules.
      // TODO: refactor these patterns and promote back to error.
      'react-hooks/set-state-in-effect': 'warn',
      'react-hooks/refs': 'warn',
    },
  },
]);

export default eslintConfig;
