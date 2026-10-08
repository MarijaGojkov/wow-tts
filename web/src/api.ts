// Types and calls for the wow-tts API. The shapes match QuestDto and QuestPartDto in WowTts.Application.

export type QuestPart = 'accept' | 'progress' | 'complete'

export interface QuestPartInfo {
  part: QuestPart
  text: string
  hasAudio: boolean
}

export interface Quest {
  id: number
  title: string
  npcName: string
  npcRace: string
  npcGender: string
  parts: QuestPartInfo[]
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

async function request(url: string): Promise<Response> {
  let response: Response
  try {
    response = await fetch(url)
  } catch {
    throw new Error(unreachable)
  }
  if (!response.ok) throw await toError(response)
  return response
}

export async function getQuests(): Promise<Quest[]> {
  return (await request('/api/quests')).json()
}

export async function getQuest(id: number): Promise<Quest> {
  return (await request(`/api/quests/${id}`)).json()
}

export function audioUrl(questId: number, part: QuestPart): string {
  return `/api/quests/${questId}/audio/${part}`
}

// Asks the API for the audio, which makes it generate and save the MP3 if it doesn't exist yet.
export async function generateAudio(questId: number, part: QuestPart): Promise<void> {
  await (await request(audioUrl(questId, part))).blob()
}
