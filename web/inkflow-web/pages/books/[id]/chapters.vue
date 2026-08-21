<script setup lang="ts">
import type { BookDetail, ChapterSummary } from '~/types/api'

const route = useRoute()
const bookId = computed(() => String(route.params.id))
const filter = ref('')
const reversed = ref(false)
const visibleCount = ref(200)
const { data, status, error } = await useAsyncData(`toc:${bookId.value}`, async () => {
  const baseURL = inkFlowApiBase()
  const [book, chapters] = await Promise.all([
    $fetch<BookDetail>(`/api/v1/books/${bookId.value}`, { baseURL }),
    $fetch<ChapterSummary[]>(`/api/v1/books/${bookId.value}/chapters`, { baseURL })
  ])
  return { book, chapters }
})

const filtered = computed(() => {
  const keyword = filter.value.trim().toLocaleLowerCase()
  let chapters = data.value?.chapters ?? []
  if (keyword) chapters = chapters.filter(chapter => chapter.title.toLocaleLowerCase().includes(keyword))
  if (reversed.value) chapters = [...chapters].reverse()
  return chapters
})
const visible = computed(() => filtered.value.slice(0, visibleCount.value))
watch([filter, reversed], () => { visibleCount.value = 200 })
useSeoMeta({ title: () => data.value ? `${data.value.book.title} · 目录` : '作品目录' })
</script>

<template>
  <div class="page page-toc">
    <div v-if="status === 'pending'" class="state-card" role="status">正在加载目录…</div>
    <div v-else-if="error || !data" class="state-card state-card--error" role="alert">目录暂时不可用。</div>
    <template v-else>
      <nav class="breadcrumb" aria-label="面包屑">
        <NuxtLink to="/">书库</NuxtLink><span>/</span><NuxtLink :to="`/books/${data.book.id}`">{{ data.book.title }}</NuxtLink><span>/</span><span>目录</span>
      </nav>
      <header class="toc-header">
        <div><p class="eyebrow">共 {{ data.chapters.length }} 章</p><h1>{{ data.book.title }}</h1><p>{{ data.book.author || '佚名' }}</p></div>
        <NuxtLink class="button button--secondary" :to="`/books/${data.book.id}`">作品详情</NuxtLink>
      </header>
      <div class="toc-tools">
        <label class="toc-search"><span class="sr-only">搜索章节</span><input v-model="filter" type="search" placeholder="搜索章节标题…"></label>
        <button class="button button--ghost" type="button" @click="reversed = !reversed">{{ reversed ? '正序' : '倒序' }}</button>
      </div>
      <ol v-if="visible.length" class="chapter-list">
        <li v-for="chapter in visible" :key="chapter.id">
          <NuxtLink :to="`/read/${data.book.id}/${chapter.id}`">
            <span>{{ chapter.title }}</span><span class="chapter-list__arrow" aria-hidden="true">›</span>
          </NuxtLink>
        </li>
      </ol>
      <div v-else class="state-card">没有匹配的章节。</div>
      <div v-if="visible.length < filtered.length" class="load-more">
        <button class="button button--secondary" type="button" @click="visibleCount += 200">继续加载（剩余 {{ filtered.length - visible.length }}）</button>
      </div>
    </template>
  </div>
</template>
