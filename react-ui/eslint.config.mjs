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
      // Require explicit return types on exported functions
      '@typescript-eslint/explicit-function-return-type': [
        'warn',
        {
          allowExpressions: true,
          allowTypedFunctionExpressions: true,
          allowHigherOrderFunctions: true,
        },
      ],
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
      // No console.log in production (warn, not error, for development)
      'no-console': ['warn', { allow: ['warn', 'error'] }],
    },
  },
]);

export default eslintConfig;
