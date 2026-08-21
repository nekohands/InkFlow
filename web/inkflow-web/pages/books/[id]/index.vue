<script setup lang="ts">
import type { BookDetail, ChapterSummary } from '~/types/api'

const route = useRoute()
const bookId = computed(() => String(route.params.id))
const { data, status, error } = await useAsyncData(`book:${bookId.value}`, async () => {
  const baseURL = inkFlowApiBase()
  const [book, chapters] = await Promise.all([
    $fetch<BookDetail>(`/api/v1/books/${bookId.value}`, { baseURL }),
    $fetch<ChapterSummary[]>(`/api/v1/books/${bookId.value}/chapters`, { baseURL })
  ])
  return { book, chapters }
})

const continueChapterId = ref<string | null>(null)
onMounted(() => {
  continueChapterId.value = localStorage.getItem(`inkflow:last:${bookId.value}`)
})
const startChapter = computed(() => {
  const chapters = data.value?.chapters ?? []
  return chapters.find(chapter => chapter.id === continueChapterId.value) ?? chapters[0] ?? null
})

useSeoMeta({
  title: () => data.value?.book.title ?? '作品详情',
  description: () => data.value?.book.description ?? '墨流作品详情'
})
</script>

<template>
  <div class="page">
    <div v-if="status === 'pending'" class="state-card" role="status">正在加载作品…</div>
    <div v-else-if="error || !data" class="state-card state-card--error" role="alert">
      <strong>作品暂时不可用</strong><NuxtLink to="/">返回书库</NuxtLink>
    </div>
    <template v-else>
      <section class="book-hero">
        <div class="book-hero__cover" aria-hidden="true">{{ data.book.title.slice(0, 1) }}</div>
        <div class="book-hero__content">
          <p class="eyebrow">{{ data.book.status || '连载状态未知' }}</p>
          <h1>{{ data.book.title }}</h1>
          <p class="book-hero__author">{{ data.book.author || '佚名' }}</p>
          <p class="book-hero__description">{{ data.book.description || '暂无作品简介。' }}</p>
          <div class="book-meta">
            <span>{{ data.chapters.length }} 章</span>
            <span v-if="data.book.latestChapter">更新至 {{ data.book.latestChapter }}</span>
          </div>
          <div class="action-row">
            <NuxtLink v-if="startChapter" class="button button--primary" :to="`/read/${data.book.id}/${startChapter.id}`">
              {{ continueChapterId ? '继续阅读' : '开始阅读' }}
            </NuxtLink>
            <span v-else class="button button--disabled">正文准备中</span>
            <NuxtLink class="button button--secondary" :to="`/books/${data.book.id}/chapters`">查看目录</NuxtLink>
          </div>
        </div>
      </section>

      <section class="content-section compact-section">
        <div class="section-heading">
          <div><p class="eyebrow">目录</p><h2>最近章节</h2></div>
          <NuxtLink class="text-link" :to="`/books/${data.book.id}/chapters`">全部 {{ data.chapters.length }} 章</NuxtLink>
        </div>
        <ol v-if="data.chapters.length" class="chapter-preview">
          <li v-for="chapter in data.chapters.slice(-8).reverse()" :key="chapter.id">
            <NuxtLink :to="`/read/${data.book.id}/${chapter.id}`">{{ chapter.title }}</NuxtLink>
          </li>
        </ol>
        <div v-else class="state-card">目录正在准备中。</div>
      </section>
    </template>
  </div>
</template>
