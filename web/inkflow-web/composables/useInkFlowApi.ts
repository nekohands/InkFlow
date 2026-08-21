import type { UseFetchOptions } from 'nuxt/app'

export function useInkFlowApi<T>(path: string | (() => string), options: UseFetchOptions<T> = {}) {
  const config = useRuntimeConfig()
  const baseURL = import.meta.server ? config.apiServerBase : config.public.apiBase
  return useFetch<T>(path, {
    baseURL,
    retry: 1,
    timeout: 8_000,
    ...options
  })
}

export function inkFlowApiBase(): string {
  const config = useRuntimeConfig()
  return import.meta.server ? config.apiServerBase : config.public.apiBase
}
