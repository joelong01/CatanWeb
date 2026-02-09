import '@testing-library/jest-dom';

/**
 * Node.js 22+ ships a built-in localStorage that shadows jsdom's implementation.
 * Without a valid --localstorage-file path the built-in's methods are undefined,
 * which breaks zustand persist middleware. Replace it with an in-memory shim so
 * jsdom (or any store code) gets a working Storage interface.
 */
if (
  typeof globalThis.localStorage !== 'undefined' &&
  typeof globalThis.localStorage.setItem !== 'function'
) {
  const store = new Map<string, string>();
  globalThis.localStorage = {
    getItem: (key: string) => store.get(key) ?? null,
    setItem: (key: string, value: string) => {
      store.set(key, value);
    },
    removeItem: (key: string) => {
      store.delete(key);
    },
    clear: () => {
      store.clear();
    },
    get length() {
      return store.size;
    },
    key: (index: number) => [...store.keys()][index] ?? null,
  } as Storage;
}
