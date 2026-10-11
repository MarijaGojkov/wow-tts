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

// What speaks a line: Azure Speech (Dragon HD Omni) or gpt-4o-mini-tts.
export type VoiceEngine = 'azure' | 'openAi'

// An engine the viewer can generate with, and whether it's set up.
export interface VoiceEngineInfo {
  id: VoiceEngine
  name: string
  available: boolean
}

// How an NPC is played with gpt-4o-mini-tts: its voice and character (accent, age, temperament).
export interface NpcCharacter {
  npcName: string
  npcGender: string
  voice: string
  character: string
  reason: string
}

// How a line was spoken: chosen by the AI voice director, the default when no AI is set up,
// or imported from another addon (voice is then where it came from, e.g. "AI VoiceOver").
// For gpt-4o-mini-tts, instructions holds the character and delivery it was given.
export interface VoicePerformance {
  voice: string
  style: string | null
  styleDegree: number
  ratePercent: number
  pitchPercent: number
  reason: string
  directedBy: 'ai' | 'default' | 'imported'
  engine: VoiceEngine
  instructions: string | null
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
  // Who says it: the quest giver, or the NPC you hand the quest in to.
  speaker: string
  performance: VoicePerformance | null
  timings: AudioTimings | null
}

// One word of a part's text (offset and length in the text) and when it's spoken, in milliseconds.
export interface WordTiming {
  offset: number
  length: number
  startMs: number
  endMs: number
}

// When each word of a part's audio is spoken: from Azure while it spoke, or from Whisper listening afterwards.
// match is the share of words the source confirmed (0 to 1).
export interface AudioTimings {
  source: 'azure' | 'whisper'
  match: number
  words: WordTiming[]
}

// An NPC of a quest and the voice cast for them.
export interface QuestNpc {
  name: string
  race: string
  gender: string
  voice: NpcVoice | null
  character: NpcCharacter | null
}

export type QuestNpcRole = 'giver' | 'ender'

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
  character: NpcCharacter | null
  // The NPC you hand the quest in to, when that's not the quest giver.
  ender: QuestNpc | null
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

// Fetched once: which engines can generate audio.
let engines: Promise<VoiceEngineInfo[]> | null = null
export function getEngines(): Promise<VoiceEngineInfo[]> {
  engines ??= request('/api/voices/engines').then((response) => response.json())
  return engines
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

// Asks the API for the audio, which makes it generate and save the MP3 with that engine if it doesn't exist yet.
export async function generateAudio(questId: number, part: QuestPart, engine: VoiceEngine): Promise<void> {
  await (await request(`${audioUrl(questId, part)}?engine=${engine}`)).blob()
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

// Forgets an NPC's voice, so the voice director casts a new one for the next audio.
export function recastVoice(questId: number, npc: QuestNpcRole, engine: VoiceEngine): Promise<void> {
  return send(`/api/quests/${questId}/voice?npc=${npc}&engine=${engine}`, 'DELETE')
}
