import { describe, expect, it } from 'vitest'
import { chapterNeighbors, clampReaderSettings, splitParagraphs } from '../utils/reader'

const chapters = [
  { id: 'a', title: '第一章', sequence: 1, revision: 1 },
  { id: 'b', title: '第二章', sequence: 2, revision: 1 },
  { id: 'c', title: '第三章', sequence: 3, revision: 1 }
]

describe('reader utilities', () => {
  it('finds previous and next chapter', () => {
    const result = chapterNeighbors(chapters, 'b')
    expect(result.previous?.id).toBe('a')
    expect(result.next?.id).toBe('c')
    expect(result.index).toBe(1)
  })

  it('clamps persisted settings to safe values', () => {
    const result = clampReaderSettings({ fontSize: 80, lineHeight: 0.5, theme: 'dark', width: 'wide' })
    expect(result.fontSize).toBe(32)
    expect(result.lineHeight).toBe(1.5)
    expect(result.theme).toBe('dark')
    expect(result.width).toBe('wide')
  })

  it('normalizes content into readable paragraphs', () => {
    expect(splitParagraphs(' 第一段\r\n\r\n第二段\n 第三段 ')).toEqual(['第一段', '第二段', '第三段'])
  })
})
