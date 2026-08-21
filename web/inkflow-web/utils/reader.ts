import type { ChapterSummary } from '~/types/api'

export type ReaderTheme = 'system' | 'light' | 'warm' | 'dark'
export type ReaderWidth = 'narrow' | 'normal' | 'wide'

export interface ReaderSettings {
  theme: ReaderTheme
  fontSize: number
  lineHeight: number
  width: ReaderWidth
}

export const defaultReaderSettings: ReaderSettings = {
  theme: 'system',
  fontSize: 20,
  lineHeight: 1.9,
  width: 'normal'
}

export function clampReaderSettings(input: Partial<ReaderSettings>): ReaderSettings {
  const themes: ReaderTheme[] = ['system', 'light', 'warm', 'dark']
  const widths: ReaderWidth[] = ['narrow', 'normal', 'wide']
  return {
    theme: themes.includes(input.theme as ReaderTheme) ? input.theme as ReaderTheme : defaultReaderSettings.theme,
    fontSize: clampNumber(input.fontSize, 16, 32, defaultReaderSettings.fontSize),
    lineHeight: clampNumber(input.lineHeight, 1.5, 2.4, defaultReaderSettings.lineHeight),
    width: widths.includes(input.width as ReaderWidth) ? input.width as ReaderWidth : defaultReaderSettings.width
  }
}

export function splitParagraphs(content: string): string[] {
  return content.replace(/\r\n/g, '\n')
    .split(/\n{1,}/)
    .map(value => value.trim())
    .filter(Boolean)
}

export function chapterNeighbors(chapters: ChapterSummary[], chapterId: string) {
  const index = chapters.findIndex(chapter => chapter.id === chapterId)
  return {
    previous: index > 0 ? chapters[index - 1] : null,
    next: index >= 0 && index < chapters.length - 1 ? chapters[index + 1] : null,
    index
  }
}

function clampNumber(value: number | undefined, min: number, max: number, fallback: number): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) return fallback
  return Math.min(max, Math.max(min, value))
}
