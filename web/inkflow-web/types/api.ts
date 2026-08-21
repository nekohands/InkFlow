export interface BookSummary {
  id: string
  title: string
  author: string
  description?: string | null
  revision: number
}

export interface BookDetail {
  id: string
  title: string
  author: string
  description?: string | null
  status: string
  latestChapter?: string | null
  revision: number
}

export interface ChapterSummary {
  id: string
  title: string
  sequence: number
  displayNumber?: number | null
  revision: number
}

export interface ChapterContent {
  chapterId: string
  title: string
  content: string
  contentVersionId: string
  qualityScore: number
  contentHash: string
}
