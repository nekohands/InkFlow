export default defineNuxtConfig({
  compatibilityDate: '2026-08-01',
  devtools: { enabled: false },
  css: ['~/assets/css/main.css'],
  app: {
    head: {
      htmlAttrs: { lang: 'zh-CN' },
      titleTemplate: '%s · 墨流 InkFlow',
      meta: [
        { name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' },
        { name: 'theme-color', content: '#f7f4ed' },
        { name: 'description', content: '墨流 InkFlow：简洁、稳定的多源小说阅读体验。' }
      ]
    }
  },
  runtimeConfig: {
    apiServerBase: 'http://localhost:8080',
    public: {
      apiBase: 'http://localhost:8080'
    }
  },
  typescript: {
    strict: true,
    typeCheck: false
  }
})
