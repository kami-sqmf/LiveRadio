// Small subset verified against the installed CS2 1.6 UI exports and stock toolkit declarations.
declare module 'cs2/api' {
  export interface Binding<T> { readonly value: T; }
  export function bindValue<T>(group: string, name: string, fallback?: T): Binding<T>;
  export function useValue<T>(binding: Binding<T>): T;
  export function trigger(group: string, name: string, ...args: unknown[]): void;
}
declare module 'cs2/ui' {
  export const Scrollable: import('react').ComponentType<import('react').PropsWithChildren<{
    vertical?: boolean; className?: string; smooth?: boolean; trackVisibility?: 'always' | 'scrollable' | 'reserve';
  }>>;
}
declare module 'cs2/input' {
  export const AutoNavigationScope: import('react').ComponentType<import('react').PropsWithChildren<{debugName?: string}>>;
  export const InputActionBarrier: import('react').ComponentType<import('react').PropsWithChildren<{disabled?: boolean; excludes?: string[]}>>;
}
