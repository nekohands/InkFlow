<script setup lang="ts">
import type { ChapterContent, ChapterSummary } from '~/types/api'
import { chapterNeighbors, clampReaderSettings, defaultReaderSettings, splitParagraphs, type ReaderSettings } from '~/utils/reader'

const route = useRoute()
const bookId = computed(() => String(route.params.bookId))
const chapterId = computed(() => String(route.params.chapterId))
const settingsOpen = ref(false)
const settings = reactive<ReaderSettings>({ ...defaultReaderSettings })

const { data, status, error } = await useAsyncData(`reader:${chapterId.value}`, async () => {
  const baseURL = inkFlowApiBase()
  const [chapters, chapter] = await Promise.all([
    $fetch<ChapterSummary[]>(`/api/v1/books/${bookId.value}/chapters`, { baseURL }),
    $fetch<ChapterContent>(`/api/v1/chapters/${chapterId.value}`, { baseURL })
  ])
  return { chapters, chapter }
}, { watch: [chapterId] })

const neighbors = computed(() => chapterNeighbors(data.value?.chapters ?? [], chapterId.value))
const paragraphs = computed(() => splitParagraphs(data.value?.chapter.content ?? ''))
const readerStyle = computed(() => ({
  '--reader-font-size': `${settings.fontSize}px`,
  '--reader-line-height': String(settings.lineHeight)
}))

function goTo(chapter: ChapterSummary | null) {
  if (chapter) navigateTo(`/read/${bookId.value}/${chapter.id}`)
}

function persistSettings() {
  if (!import.meta.client) return
  localStorage.setItem('inkflow:reader-settings', JSON.stringify(settings))
}

watch(settings, persistSettings, { deep: true })
watch(chapterId, () => {
  if (import.meta.client) localStorage.setItem(`inkflow:last:${bookId.value}`, chapterId.value)
})

function onKeydown(event: KeyboardEvent) {
  if (event.target instanceof HTMLInputElement || event.target instanceof HTMLSelectElement || event.target instanceof HTMLButtonElement) return
  if (event.key === 'ArrowLeft') goTo(neighbors.value.previous)
  if (event.key === 'ArrowRight') goTo(neighbors.value.next)
}

onMounted(() => {
  const stored = localStorage.getItem('inkflow:reader-settings')
  if (stored) {
    try { Object.assign(settings, clampReaderSettings(JSON.parse(stored))) } catch { /* ignore invalid local state */ }
  }
  localStorage.setItem(`inkflow:last:${bookId.value}`, chapterId.value)
  window.addEventListener('keydown', onKeydown)
})
onBeforeUnmount(() => window.removeEventListener('keydown', onKeydown))

useSeoMeta({ title: () => data.value?.chapter.title ?? '阅读' })
</script>

<template>
  <div class="reader-page" :data-theme="settings.theme" :data-width="settings.width" :style="readerStyle">
    <div class="reader-toolbar" aria-label="阅读工具栏">
      <NuxtLink class="reader-tool" :to="`/books/${bookId}/chapters`">目录</NuxtLink>
      <button class="reader-tool" type="button" :aria-expanded="settingsOpen" @click="settingsOpen = !settingsOpen">阅读设置</button>
    </div>

    <aside v-if="settingsOpen" class="reader-settings" aria-label="阅读设置">
      <div class="setting-row"><span>主题</span><div class="segmented"><button v-for="theme in ['system','light','warm','dark']" :key="theme" type="button" :aria-pressed="settings.theme === theme" @click="settings.theme = theme as ReaderSettings['theme']">{{ {system:'跟随系统',light:'明亮',warm:'暖色',dark:'夜间'}[theme] }}</button></div></div>
      <div class="setting-row"><label for="font-size">字号 {{ settings.fontSize }}</label><input id="font-size" v-model.number="settings.fontSize" type="range" min="16" max="32" step="1"></div>
      <div class="setting-row"><label for="line-height">行距 {{ settings.lineHeight.toFixed(1) }}</label><input id="line-height" v-model.number="settings.lineHeight" type="range" min="1.5" max="2.4" step="0.1"></div>
      <div class="setting-row"><span>版心</span><div class="segmented"><button v-for="width in ['narrow','normal','wide']" :key="width" type="button" :aria-pressed="settings.width === width" @click="settings.width = width as ReaderSettings['width']">{{ {narrow:'窄',normal:'标准',wide:'宽'}[width] }}</button></div></div>
    </aside>

    <div v-if="status === 'pending'" class="reader-state" role="status">正在翻开这一章…</div>
    <div v-else-if="error || !data" class="reader-state reader-state--error" role="alert"><strong>这一章暂时无法打开</strong><NuxtLink :to="`/books/${bookId}/chapters`">返回目录</NuxtLink></div>
    <article v-else class="reader-article">
      <header class="reader-heading">
        <p class="eyebrow">第 {{ neighbors.index + 1 }} / {{ data.chapters.length }} 章</p>
        <h1>{{ data.chapter.title }}</h1>
      </header>
      <div class="reader-content">
        <p v-for="(paragraph, index) in paragraphs" :key="index">{{ paragraph }}</p>
      </div>
      <nav class="reader-nav" aria-label="章节导航">
        <button class="button button--secondary" type="button" :disabled="!neighbors.previous" @click="goTo(neighbors.previous)">← 上一章</button>
        <NuxtLink class="button button--secondary" :to="`/books/${bookId}/chapters`">目录</NuxtLink>
        <button class="button button--primary" type="button" :disabled="!neighbors.next" @click="goTo(neighbors.next)">下一章 →</button>
      </nav>
    </article>
  </div>
</template>
