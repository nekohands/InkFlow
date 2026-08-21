<template>
  <header class="site-header">
    <a class="skip-link" href="#main-content">跳到正文</a>
    <div class="site-header__inner">
      <NuxtLink class="brand" to="/" aria-label="墨流首页">
        <span class="brand__mark" aria-hidden="true">墨</span>
        <span>
          <strong>墨流</strong>
          <small>InkFlow</small>
        </span>
      </NuxtLink>
      <nav class="top-nav" aria-label="主导航">
        <NuxtLink to="/">发现</NuxtLink>
        <a href="/legado/book-source.json">阅读 3.0</a>
      </nav>
    </div>
  </header>
</template>
