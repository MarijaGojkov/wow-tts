import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { getQuest, getQuests, type Quest } from './api'
import { audioCount, errorText, type AudioFilter } from './labels'
import { QuestDetail } from './QuestDetail'
import { QuestList } from './QuestList'

const filters: { value: AudioFilter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'done', label: 'With audio' },
  { value: 'missing', label: 'Missing audio' },
]

function matchesFilter(quest: Quest, filter: AudioFilter): boolean {
  const count = audioCount(quest)
  if (filter === 'done') return count > 0
  if (filter === 'missing') return count < quest.parts.length
  return true
}

function matchesSearch(quest: Quest, search: string): boolean {
  const term = search.trim().toLowerCase()
  if (!term) return true
  return [String(quest.id), quest.title, quest.npcName, quest.npcRace].some((value) =>
    value.toLowerCase().includes(term),
  )
}

export default function App() {
  const [quests, setQuests] = useState<Quest[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState<AudioFilter>('all')
  const contentRef = useRef<HTMLDivElement>(null)

  // A newly picked quest starts at the top, instead of where the previous one was scrolled to.
  useEffect(() => {
    contentRef.current?.scrollTo({ top: 0 })
  }, [selectedId])

  // Fetches all quests and selects the first one if nothing is selected yet.
  const load = useCallback(() => {
    getQuests().then(
      (all) => {
        setQuests(all)
        setSelectedId((current) => current ?? all[0]?.id ?? null)
      },
      (e) => setError(errorText(e, 'Loading the quests failed.')),
    )
  }, [])

  useEffect(load, [load])

  function refresh() {
    setError(null)
    load()
  }

  // After generating audio, reload just that quest so its parts show the new audio.
  const reloadQuest = useCallback(async (id: number) => {
    const updated = await getQuest(id)
    setQuests((current) => current?.map((quest) => (quest.id === id ? updated : quest)) ?? null)
  }, [])

  const visible = useMemo(
    () => (quests ?? []).filter((quest) => matchesFilter(quest, filter) && matchesSearch(quest, search)),
    [quests, filter, search],
  )

  const selected = quests?.find((quest) => quest.id === selectedId) ?? null
  const totalParts = quests?.reduce((sum, quest) => sum + quest.parts.length, 0) ?? 0
  const partsWithAudio = quests?.reduce((sum, quest) => sum + audioCount(quest), 0) ?? 0

  return (
    <div className="app">
      <header className="top">
        <div>
          <p className="eyebrow">wow-tts</p>
          <h1>Quest Voices</h1>
        </div>
        {quests && (
          <dl className="stats">
            <div>
              <dt>Quests</dt>
              <dd>{quests.length}</dd>
            </div>
            <div>
              <dt>Parts with audio</dt>
              <dd>
                {partsWithAudio} / {totalParts}
              </dd>
            </div>
          </dl>
        )}
        <button type="button" className="refresh" onClick={refresh}>
          Refresh
        </button>
      </header>

      {error && <p className="error banner">{error}</p>}

      {quests && (
        <main className="layout">
          <aside className="sidebar">
            <input
              id="search"
              type="search"
              className="search"
              placeholder="Search by title, NPC, race or ID"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Search quests"
            />
            <div className="filters" role="group" aria-label="Filter by audio">
              {filters.map((option) => (
                <button
                  key={option.value}
                  type="button"
                  className={option.value === filter ? 'filter active' : 'filter'}
                  onClick={() => setFilter(option.value)}
                  aria-pressed={option.value === filter}
                >
                  {option.label}
                </button>
              ))}
            </div>
            <QuestList quests={visible} selectedId={selectedId} onSelect={setSelectedId} />
          </aside>

          <div className="content" ref={contentRef}>
            {selected ? (
              <QuestDetail quest={selected} onChanged={() => reloadQuest(selected.id)} />
            ) : (
              <p className="empty">Pick a quest to see its text and audio.</p>
            )}
          </div>
        </main>
      )}

      {!quests && !error && <p className="empty">Loading quests…</p>}
    </div>
  )
}
