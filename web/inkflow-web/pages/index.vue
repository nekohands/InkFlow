<script setup lang="ts">
import type { BookSummary } from '~/types/api'

const route = useRoute()
const searchInput = ref(typeof route.query.q === 'string' ? route.query.q : '')
const query = computed(() => typeof route.query.q === 'string' ? route.query.q.trim() : '')
const { data: books, status, error } = await useAsyncData<BookSummary[]>(
  'home-books',
  () => $fetch<BookSummary[]>('/api/v1/search', {
    baseURL: inkFlowApiBase(),
    query: { q: query.value }
  }),
  {
    watch: [query],
    default: () => []
  }
)

useSeoMeta({ title: query.value ? `搜索 ${query.value}` : '发现好故事' })

async function submitSearch() {
  const q = searchInput.value.trim()
  await navigateTo(q ? { path: '/', query: { q } } : { path: '/' })
}
</script>

<template>
  <div class="page page-home">
    <section class="hero" aria-labelledby="hero-title">
      <p class="eyebrow">多源聚合 · 稳定阅读</p>
      <h1 id="hero-title">让找书和阅读，都简单一点。</h1>
      <p class="hero__lead">墨流把来源切换、内容选优和追更留在后台，你只需要找到想读的书。</p>
      <form class="search-box" role="search" @submit.prevent="submitSearch">
        <label class="sr-only" for="book-search">搜索书名或作者</label>
        <input id="book-search" v-model="searchInput" type="search" autocomplete="off" placeholder="搜索书名、作者…">
        <button class="button button--primary" type="submit">搜索</button>
      </form>
    </section>

    <section class="content-section" aria-labelledby="results-title">
      <div class="section-heading">
        <div>
          <p class="eyebrow">{{ query ? '搜索结果' : '最近更新' }}</p>
          <h2 id="results-title">{{ query ? `“${query}”` : '继续发现' }}</h2>
        </div>
        <span v-if="status === 'success'" class="muted">{{ books.length }} 本</span>
      </div>

      <div v-if="status === 'pending'" class="state-card" role="status">正在整理书库…</div>
      <div v-else-if="error" class="state-card state-card--error" role="alert">
        <strong>暂时无法加载书库</strong><span>请稍后刷新页面重试。</span>
      </div>
      <div v-else-if="books.length === 0" class="state-card">
        <strong>{{ query ? '没有找到匹配的作品' : '书库还是空的' }}</strong>
        <span>{{ query ? '换一个书名或作者试试。' : '内容导入后会出现在这里。' }}</span>
      </div>
      <div v-else class="book-grid">
        <NuxtLink v-for="book in books" :key="book.id" class="book-card" :to="`/books/${book.id}`">
          <div class="book-card__cover" aria-hidden="true">{{ book.title.slice(0, 1) }}</div>
          <div class="book-card__body">
            <h3>{{ book.title }}</h3>
            <p class="book-card__author">{{ book.author || '佚名' }}</p>
            <p class="book-card__description">{{ book.description || '暂无简介' }}</p>
            <span class="text-link">查看作品 <span aria-hidden="true">→</span></span>
          </div>
        </NuxtLink>
      </div>
    </section>
  </div>
</template>
