import type { QuestPart, QuestPartStatus, QuestSide, QuestSource } from './api'

export const sourceLabels: Record<QuestSource, string> = {
  sample: 'Sample quest',
  cMangos: 'cMaNGOS (vanilla 1.12)',
  captured: 'Captured in game',
}

export const sideLabels: Record<QuestSide, string> = {
  both: 'Both factions',
  alliance: 'Alliance',
  horde: 'Horde',
}

export const partLabels: Record<QuestPart, { name: string; when: string }> = {
  accept: { name: 'Accept', when: 'When you take the quest' },
  progress: { name: 'Progress', when: 'When you return before finishing' },
  complete: { name: 'Complete', when: 'When you hand it in' },
}

// Level ranges for the level filter.
export const levelRanges: { label: string; min: number | null; max: number | null }[] = [
  { label: 'All levels', min: null, max: null },
  { label: '1–10', min: 1, max: 10 },
  { label: '11–20', min: 11, max: 20 },
  { label: '21–30', min: 21, max: 30 },
  { label: '31–40', min: 31, max: 40 },
  { label: '41–50', min: 41, max: 50 },
  { label: '51–60', min: 51, max: 60 },
]

export function audioCount(parts: QuestPartStatus[]): number {
  return parts.filter((part) => part.hasAudio).length
}

export function errorText(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback
}

// "NightElf" -> "Night Elf"
export function splitWords(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}
