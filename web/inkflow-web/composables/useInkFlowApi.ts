export function inkFlowApiBase(): string {
  const config = useRuntimeConfig()
  return import.meta.server ? config.apiServerBase : config.public.apiBase
}
