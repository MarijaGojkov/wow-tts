// Types and calls for the wow-tts API. The shapes match the DTOs in WowTts.Application.

export type QuestPart = 'accept' | 'progress' | 'complete'

// Where the quest text came from (QuestSource in WowTts.Domain).
export type QuestSource = 'sample' | 'cMangos' | 'captured'

export type QuestSide = 'both' | 'alliance' | 'horde'

export type AudioFilter = 'all' | 'done' | 'missing'

export interface MapPoint {
  x: number
  y: number
}

export interface QuestPartStatus {
  part: QuestPart
  hasAudio: boolean
}

// How a line was spoken: chosen by the AI voice director, or the default when no AI is set up.
export interface VoicePerformance {
  voice: string
  style: string | null
  styleDegree: number
  ratePercent: number
  pitchPercent: number
  reason: string
  directedBy: 'ai' | 'default'
}

// The voice cast for an NPC, used for all their quests.
export interface NpcVoice {
  npcName: string
  npcGender: string
  voice: string
  reason: string
}

export interface QuestPartInfo extends QuestPartStatus {
  text: string
  performance: VoicePerformance | null
}

// Fields shared by the list and the detail view.
interface QuestBase {
  id: number
  title: string
  npcName: string
  source: QuestSource
  level: number | null
  minLevel: number | null
  side: QuestSide
  categoryId: number | null
  categoryName: string | null
  mapZoneId: number | null
  mapZoneName: string | null
  mapPosition: MapPoint | null
}

// A quest in a list, without its text.
export interface QuestSummary extends QuestBase {
  parts: QuestPartStatus[]
}

// A quest with all its text.
export interface Quest extends QuestBase {
  npcRace: string
  npcGender: string
  voice: NpcVoice | null
  parts: QuestPartInfo[]
}

export interface QuestSearchResult {
  items: QuestSummary[]
  total: number
  page: number
  pageSize: number
}

export interface QuestStats {
  quests: number
  parts: number
  partsWithAudio: number
}

export interface MapZone {
  id: number
  name: string
  type: 'continent' | 'zone'
  parentId: number | null
  rectOnParent: { x: number; y: number; width: number; height: number } | null
  questCount: number
  parts: number
  partsWithAudio: number
}

// A quest giver's spot on a map, in percent of that map.
export interface MapMarker {
  questId: number
  title: string
  npcName: string
  level: number | null
  side: QuestSide
  mapZoneId: number | null
  position: MapPoint
  parts: number
  partsWithAudio: number
}

export interface QuestFilters {
  search: string
  mapZoneId: number | null
  side: QuestSide
  audio: AudioFilter
  minLevel: number | null
  maxLevel: number | null
}

const unreachable =
  "Can't reach the API. Start it with: dotnet run --project src/WowTts.Api (it runs on http://localhost:5219)."

// Turns a failed response into a readable message. The API sends ProblemDetails ({ title, detail }) for its errors.
async function toError(response: Response): Promise<Error> {
  const type = response.headers.get('content-type') ?? ''
  if (type.includes('json')) {
    const problem = (await response.json()) as { title?: string; detail?: string }
    return new Error(problem.detail ?? problem.title ?? `Request failed (${response.status}).`)
  }
  // The Vite proxy answers with a plain 5xx when the API isn't running.
  return new Error(response.status >= 500 ? unreachable : `Request failed (${response.status}).`)
}

async function request(url: string, signal?: AbortSignal): Promise<Response> {
  let response: Response
  try {
    response = await fetch(url, { signal })
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') throw e
    throw new Error(unreachable)
  }
  if (!response.ok) throw await toError(response)
  return response
}

function filterParams(filters: QuestFilters): URLSearchParams {
  const params = new URLSearchParams()
  if (filters.search.trim()) params.set('search', filters.search.trim())
  if (filters.mapZoneId !== null) params.set('mapZoneId', String(filters.mapZoneId))
  if (filters.side !== 'both') params.set('side', filters.side)
  if (filters.audio !== 'all') params.set('audio', filters.audio)
  if (filters.minLevel !== null) params.set('minLevel', String(filters.minLevel))
  if (filters.maxLevel !== null) params.set('maxLevel', String(filters.maxLevel))
  return params
}

export async function searchQuests(
  filters: QuestFilters,
  page: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<QuestSearchResult> {
  const params = filterParams(filters)
  params.set('page', String(page))
  params.set('pageSize', String(pageSize))
  return (await request(`/api/quests?${params}`, signal)).json()
}

// Every matching quest giver on one map (filters.mapZoneId is the map to draw).
export async function getMapMarkers(filters: QuestFilters, signal?: AbortSignal): Promise<MapMarker[]> {
  return (await request(`/api/map/markers?${filterParams(filters)}`, signal)).json()
}

export async function getQuest(id: number): Promise<Quest> {
  return (await request(`/api/quests/${id}`)).json()
}

export async function getStats(): Promise<QuestStats> {
  return (await request('/api/quests/stats')).json()
}

export async function getMapZones(): Promise<MapZone[]> {
  return (await request('/api/map/zones')).json()
}

export function audioUrl(questId: number, part: QuestPart): string {
  return `/api/quests/${questId}/audio/${part}`
}

// Asks the API for the audio, which makes it generate and save the MP3 if it doesn't exist yet.
export async function generateAudio(questId: number, part: QuestPart): Promise<void> {
  await (await request(audioUrl(questId, part))).blob()
}

async function send(url: string, method: string): Promise<void> {
  let response: Response
  try {
    response = await fetch(url, { method })
  } catch {
    throw new Error(unreachable)
  }
  if (!response.ok) throw await toError(response)
}

// Deletes a part's audio, so it can be generated again with a fresh performance.
export function deleteAudio(questId: number, part: QuestPart): Promise<void> {
  return send(audioUrl(questId, part), 'DELETE')
}

// Forgets the NPC's voice, so the voice director casts a new one for the next audio.
export function recastVoice(questId: number): Promise<void> {
  return send(`/api/quests/${questId}/voice`, 'DELETE')
}
