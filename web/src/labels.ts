import type { Quest, QuestPart } from './api'

export type AudioFilter = 'all' | 'done' | 'missing'

export function audioCount(quest: Quest): number {
  return quest.parts.filter((part) => part.hasAudio).length
}

export function errorText(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback
}

export const partLabels: Record<QuestPart, { name: string; when: string }> = {
  accept: { name: 'Accept', when: 'When you take the quest' },
  progress: { name: 'Progress', when: 'When you return before finishing' },
  complete: { name: 'Complete', when: 'When you hand it in' },
}

// "NightElf" -> "Night Elf"
export function splitWords(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}
