import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  getMapZones,
  getQuest,
  getStats,
  searchQuests,
  type AudioFilter,
  type MapZone,
  type Quest,
  type QuestFilters,
  type QuestSide,
  type QuestStats,
  type QuestSummary,
} from './api'
import { errorText, levelRanges, sideLabels } from './labels'
import { QuestDetail } from './QuestDetail'
import { QuestList } from './QuestList'
import { WorldMap } from './WorldMap'

const pageSize = 50

const audioFilters: { value: AudioFilter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'done', label: 'With audio' },
  { value: 'missing', label: 'Missing audio' },
]

const initialFilters: QuestFilters = {
  search: '',
  mapZoneId: null,
  side: 'both',
  audio: 'all',
  minLevel: null,
  maxLevel: null,
}

// The loaded pages for one set of filters. Key tells which filters they belong to.
interface Results {
  key: string
  items: QuestSummary[]
  total: number
  pages: number
}

export default function App() {
  const [stats, setStats] = useState<QuestStats | null>(null)
  const [zones, setZones] = useState<MapZone[]>([])
  const [searchInput, setSearchInput] = useState('')
  const [filters, setFilters] = useState<QuestFilters>(initialFilters)
  const [results, setResults] = useState<Results | null>(null)
  const [loadingMore, setLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [detail, setDetail] = useState<Quest | null>(null)
  const [view, setView] = useState<'quest' | 'map'>('quest')
  const contentRef = useRef<HTMLDivElement>(null)
  const sentinelRef = useRef<HTMLDivElement>(null)

  const filtersKey = JSON.stringify(filters)
  const loading = results?.key !== filtersKey

  const showError = useCallback((e: unknown) => setError(errorText(e, 'Something went wrong.')), [])

  const loadStats = useCallback(() => {
    getStats().then(setStats, showError)
  }, [showError])

  // Stats and the zone list, once.
  useEffect(() => {
    loadStats()
    getMapZones().then(setZones, showError)
  }, [loadStats, showError])

  // Wait until typing pauses before searching.
  useEffect(() => {
    const timer = setTimeout(() => setFilters((current) => ({ ...current, search: searchInput })), 300)
    return () => clearTimeout(timer)
  }, [searchInput])

  // The first page whenever the filters change.
  useEffect(() => {
    const controller = new AbortController()
    searchQuests(filters, 1, pageSize, controller.signal).then(
      (page) => {
        setResults({ key: JSON.stringify(filters), items: page.items, total: page.total, pages: 1 })
        // Keep the selected quest if it's still in the list, otherwise show the first match.
        setSelectedId((current) =>
          page.items.some((item) => item.id === current) ? current : (page.items[0]?.id ?? current),
        )
        setError(null)
      },
      (e) => {
        if (!controller.signal.aborted) showError(e)
      },
    )
    return () => controller.abort()
  }, [filters, showError])

  const canLoadMore = !loading && results !== null && results.items.length < results.total

  const loadMore = useCallback(() => {
    if (!canLoadMore || loadingMore || !results) return
    setLoadingMore(true)
    searchQuests(filters, results.pages + 1, pageSize)
      .then((page) => {
        setResults((current) =>
          current && current.key === results.key
            ? { ...current, items: [...current.items, ...page.items], pages: current.pages + 1 }
            : current,
        )
      }, showError)
      .finally(() => setLoadingMore(false))
  }, [canLoadMore, loadingMore, results, filters, showError])

  // Load the next page when the end of the list scrolls into view.
  useEffect(() => {
    const sentinel = sentinelRef.current
    if (!sentinel) return
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((entry) => entry.isIntersecting)) loadMore()
    })
    observer.observe(sentinel)
    return () => observer.disconnect()
  }, [loadMore])

  // The selected quest's full text.
  useEffect(() => {
    if (selectedId === null) return
    getQuest(selectedId).then(setDetail, showError)
  }, [selectedId, showError])

  // A newly picked quest starts at the top, instead of where the previous one was scrolled to.
  useEffect(() => {
    contentRef.current?.scrollTo({ top: 0 })
  }, [selectedId])

  // After generating audio: reload the quest, update its row in the list, and refresh the totals.
  const onAudioGenerated = useCallback(async () => {
    if (selectedId === null) return
    const updated = await getQuest(selectedId)
    setDetail(updated)
    setResults((current) =>
      current && {
        ...current,
        items: current.items.map((item) =>
          item.id === updated.id
            ? { ...item, parts: updated.parts.map(({ part, hasAudio }) => ({ part, hasAudio })) }
            : item,
        ),
      },
    )
    loadStats()
  }, [selectedId, loadStats])

  function updateFilters(change: Partial<QuestFilters>) {
    setFilters((current) => ({ ...current, ...change }))
  }

  // Zones that have quests, grouped by continent for the zone filter.
  const zoneGroups = useMemo(
    () =>
      zones
        .filter((zone) => zone.type === 'continent')
        .map((continent) => ({
          continent,
          zones: zones
            .filter((zone) => zone.parentId === continent.id && zone.questCount > 0)
            .sort((a, b) => a.name.localeCompare(b.name)),
        })),
    [zones],
  )

  const levelIndex = levelRanges.findIndex(
    (range) => range.min === filters.minLevel && range.max === filters.maxLevel,
  )
  const shownDetail = detail?.id === selectedId ? detail : null

  return (
    <div className="app">
      <header className="top">
        <div>
          <p className="eyebrow">wow-tts</p>
          <h1>Quest Voices</h1>
        </div>
        {stats && (
          <dl className="stats">
            <div>
              <dt>Quests</dt>
              <dd>{stats.quests.toLocaleString()}</dd>
            </div>
            <div>
              <dt>Parts with audio</dt>
              <dd>
                {stats.partsWithAudio.toLocaleString()} / {stats.parts.toLocaleString()}
              </dd>
            </div>
          </dl>
        )}
      </header>

      {error && <p className="error banner">{error}</p>}

      {stats?.quests === 0 && (
        <p className="empty">
          No quests yet. Import them from the repo folder with:{' '}
          <code>dotnet run --project tools/WowTts.Importer -- gamedata</code>, then <code>cmangos</code>.
        </p>
      )}

      <main className="layout">
        <aside className="sidebar">
          <input
            id="search"
            type="search"
            className="search"
            placeholder="Search by title, NPC, zone or ID"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            aria-label="Search quests"
          />

          <div className="selects">
            <select
              id="zone"
              aria-label="Zone"
              value={filters.mapZoneId ?? ''}
              onChange={(e) => updateFilters({ mapZoneId: e.target.value ? Number(e.target.value) : null })}
            >
              <option value="">All zones</option>
              {zoneGroups.map(({ continent, zones: inContinent }) => (
                <optgroup key={continent.id} label={continent.name}>
                  <option value={continent.id}>
                    All of {continent.name} ({continent.questCount})
                  </option>
                  {inContinent.map((zone) => (
                    <option key={zone.id} value={zone.id}>
                      {zone.name} ({zone.questCount})
                    </option>
                  ))}
                </optgroup>
              ))}
            </select>
            <select
              id="side"
              aria-label="Faction"
              value={filters.side}
              onChange={(e) => updateFilters({ side: e.target.value as QuestSide })}
            >
              {(Object.keys(sideLabels) as QuestSide[]).map((side) => (
                <option key={side} value={side}>
                  {side === 'both' ? 'Any faction' : sideLabels[side]}
                </option>
              ))}
            </select>
            <select
              id="level"
              aria-label="Level"
              value={levelIndex}
              onChange={(e) => {
                const range = levelRanges[Number(e.target.value)]
                updateFilters({ minLevel: range.min, maxLevel: range.max })
              }}
            >
              {levelRanges.map((range, index) => (
                <option key={range.label} value={index}>
                  {range.label}
                </option>
              ))}
            </select>
          </div>

          <div className="filters" role="group" aria-label="Filter by audio">
            {audioFilters.map((option) => (
              <button
                key={option.value}
                type="button"
                className={option.value === filters.audio ? 'filter active' : 'filter'}
                onClick={() => updateFilters({ audio: option.value })}
                aria-pressed={option.value === filters.audio}
              >
                {option.label}
              </button>
            ))}
          </div>

          <p className="result-count">
            {loading ? 'Searching…' : `${results?.total.toLocaleString() ?? 0} quests`}
          </p>

          <div className={loading ? 'list-scroll stale' : 'list-scroll'}>
            {results && results.items.length > 0 ? (
              <QuestList quests={results.items} selectedId={selectedId} onSelect={setSelectedId} />
            ) : (
              !loading && <p className="empty">No quests match.</p>
            )}
            <div ref={sentinelRef} className="sentinel">
              {canLoadMore && (
                <button type="button" className="load-more" onClick={loadMore} disabled={loadingMore}>
                  {loadingMore ? 'Loading…' : `Load more (${results.total - results.items.length} left)`}
                </button>
              )}
            </div>
          </div>
        </aside>

        <div className={`workspace show-${view}`}>
          {/* On narrower screens the map and the quest share the space, so tabs switch between them. */}
          <div className="view-tabs" role="tablist" aria-label="View">
            <button
              type="button"
              role="tab"
              aria-selected={view === 'quest'}
              className={view === 'quest' ? 'view-tab active' : 'view-tab'}
              onClick={() => setView('quest')}
            >
              Quest
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={view === 'map'}
              className={view === 'map' ? 'view-tab active' : 'view-tab'}
              onClick={() => setView('map')}
            >
              Map
            </button>
          </div>

          <section className="map-pane" aria-label="World map">
            <WorldMap
              zones={zones}
              filters={filters}
              selectedQuestId={selectedId}
              onSelectZone={(mapZoneId) => updateFilters({ mapZoneId })}
              onSelectQuest={(questId) => {
                setSelectedId(questId)
                setView('quest')
              }}
            />
          </section>

          <section className="content" ref={contentRef} aria-label="Quest">
            {shownDetail ? (
              <QuestDetail quest={shownDetail} onChanged={onAudioGenerated} />
            ) : (
              <p className="empty">{selectedId === null ? 'Pick a quest to see its text and audio.' : 'Loading quest…'}</p>
            )}
          </section>
        </div>
      </main>
    </div>
  )
}
