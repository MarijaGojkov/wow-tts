import { useEffect, useMemo, useState, type CSSProperties } from 'react'
import { getMapMarkers, type MapMarker, type MapZone, type QuestFilters } from './api'
import { errorText } from './labels'

// The drawing area is 1500 x 1000: the same 3:2 shape as the game's map images,
// and as the world rectangle each map shows (e.g. Elwynn Forest is 3,470 x 2,314 world units).
const WIDTH = 1500
const HEIGHT = 1000
const toX = (percent: number) => (percent / 100) * WIDTH
const toY = (percent: number) => (percent / 100) * HEIGHT

interface WorldMapProps {
  zones: MapZone[]
  // The list's filters. mapZoneId is the zone or continent picked in the list or on the map.
  filters: QuestFilters
  selectedQuestId: number | null
  onSelectZone: (mapZoneId: number | null) => void
  onSelectQuest: (questId: number) => void
}

// Quest givers standing at the same spot, e.g. one NPC with several quests.
interface Cluster {
  key: string
  x: number
  y: number
  markers: MapMarker[]
}

export function WorldMap({ zones, filters, selectedQuestId, onSelectZone, onSelectQuest }: WorldMapProps) {
  const byId = useMemo(() => new Map(zones.map((zone) => [zone.id, zone])), [zones])
  const continents = zones.filter((zone) => zone.type === 'continent')
  const [lastContinentId, setLastContinentId] = useState(1415)

  // Which map to draw: the picked zone, the picked continent, or the continent shown last.
  const picked = filters.mapZoneId !== null ? (byId.get(filters.mapZoneId) ?? null) : null
  const continentId = picked ? (picked.type === 'continent' ? picked.id : (picked.parentId ?? lastContinentId)) : lastContinentId
  const mapId = picked?.id ?? continentId
  const map = byId.get(mapId)
  const isZone = map?.type === 'zone'

  const [loaded, setLoaded] = useState<{ key: string; markers: MapMarker[] } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [missingImages, setMissingImages] = useState<ReadonlySet<number>>(new Set())
  const [hoverKey, setHoverKey] = useState<string | null>(null)
  const [pinnedKey, setPinnedKey] = useState<string | null>(null)
  const [hoverZoneId, setHoverZoneId] = useState<number | null>(null)

  // The same filters as the list, but for the map being drawn.
  const markerFilters = useMemo(() => ({ ...filters, mapZoneId: mapId }), [filters, mapId])
  const markerKey = JSON.stringify(markerFilters)

  useEffect(() => {
    const controller = new AbortController()
    getMapMarkers(markerFilters, controller.signal).then(
      (markers) => {
        setLoaded({ key: JSON.stringify(markerFilters), markers })
        setError(null)
      },
      (e) => {
        if (!controller.signal.aborted) setError(errorText(e, 'Loading the map failed.'))
      },
    )
    return () => controller.abort()
  }, [markerFilters])

  const markers = useMemo(() => (loaded?.key === markerKey ? loaded.markers : []), [loaded, markerKey])
  const loading = loaded?.key !== markerKey

  // Group markers that sit on the same spot, so one NPC with five quests is one dot with a count.
  const clusters = useMemo(() => {
    const groups = new Map<string, Cluster>()
    for (const marker of markers) {
      const key = `${marker.position.x.toFixed(1)},${marker.position.y.toFixed(1)}`
      const group = groups.get(key) ?? { key, x: marker.position.x, y: marker.position.y, markers: [] }
      group.markers.push(marker)
      groups.set(key, group)
    }
    return [...groups.values()]
  }, [markers])

  // How many matching quests each zone has, for the zone labels on a continent.
  const countByZone = useMemo(() => {
    const counts = new Map<number, number>()
    for (const marker of markers) {
      if (marker.mapZoneId !== null) counts.set(marker.mapZoneId, (counts.get(marker.mapZoneId) ?? 0) + 1)
    }
    return counts
  }, [markers])

  // Zones of this continent, biggest first, so small ones (cities) are drawn on top and stay clickable.
  const continentZones = useMemo(
    () =>
      zones
        .filter((zone) => zone.parentId === continentId && zone.rectOnParent)
        .sort((a, b) => b.rectOnParent!.width * b.rectOnParent!.height - a.rectOnParent!.width * a.rectOnParent!.height),
    [zones, continentId],
  )

  // A continent is mostly sea, so its view is cropped to the area its zones cover. A zone shows its whole map.
  const view = useMemo(() => {
    if (isZone || continentZones.length === 0) return { x: 0, y: 0, width: WIDTH, height: HEIGHT }
    const rects = continentZones.map((zone) => zone.rectOnParent!)
    const left = toX(Math.min(...rects.map((r) => r.x)))
    const right = toX(Math.max(...rects.map((r) => r.x + r.width)))
    const top = toY(Math.min(...rects.map((r) => r.y)))
    const bottom = toY(Math.max(...rects.map((r) => r.y + r.height)))
    const padding = Math.max(right - left, bottom - top) * 0.04
    const x = Math.max(0, left - padding)
    const y = Math.max(0, top - padding)
    return {
      x,
      y,
      width: Math.min(WIDTH, right + padding) - x,
      height: Math.min(HEIGHT, bottom + padding) - y,
    }
  }, [isZone, continentZones])

  // Text and dot sizes follow the visible area, so they look the same at any crop.
  const unit = view.height / 1000
  const labelSize = view.height * 0.0165

  const activeKey = pinnedKey ?? hoverKey
  const activeCluster = isZone ? clusters.find((cluster) => cluster.key === activeKey) : undefined
  const selectedMarker = markers.find((marker) => marker.questId === selectedQuestId)
  const hasImage = !missingImages.has(mapId)

  function showContinent(id: number) {
    setLastContinentId(id)
    setPinnedKey(null)
    onSelectZone(id)
  }

  function showZone(id: number) {
    setLastContinentId(continentId)
    setPinnedKey(null)
    onSelectZone(id)
  }

  if (!map) {
    return (
      <div className="map-empty">
        {zones.length === 0
          ? 'No map data yet. Import it with: dotnet run --project tools/WowTts.Importer -- gamedata'
          : 'Loading the map…'}
      </div>
    )
  }

  return (
    <div className="world-map">
      <div className="map-toolbar">
        <div className="map-tabs" role="group" aria-label="Continent">
          {continents.map((continent) => (
            <button
              key={continent.id}
              type="button"
              className={continent.id === continentId ? 'map-tab active' : 'map-tab'}
              aria-pressed={continent.id === continentId}
              onClick={() => showContinent(continent.id)}
            >
              {continent.name}
            </button>
          ))}
        </div>
        <p className="map-title">
          {isZone && (
            <button type="button" className="map-back" onClick={() => showContinent(continentId)}>
              ← {byId.get(continentId)?.name}
            </button>
          )}
          <strong>{map.name}</strong>
          <span className="map-count">
            {loading ? 'Loading…' : `${markers.length.toLocaleString()} quests · ${clusters.length.toLocaleString()} spots`}
          </span>
        </p>
      </div>

      {error && <p className="error">{error}</p>}

      <div
        className={loading ? 'map-frame stale' : 'map-frame'}
        style={{ '--map-ratio': view.width / view.height } as CSSProperties}
        onMouseLeave={() => setHoverKey(null)}
      >
        <svg viewBox={`${view.x} ${view.y} ${view.width} ${view.height}`} role="img" aria-label={`Map of ${map.name}`}>
          {hasImage ? (
            <image
              href={`/maps/${mapId}.png`}
              width={WIDTH}
              height={HEIGHT}
              preserveAspectRatio="none"
              onError={() => setMissingImages((current) => new Set(current).add(mapId))}
            />
          ) : (
            <DrawnBackground
              rects={isZone ? [] : continentZones.map((zone) => zone.rectOnParent!)}
              name={map.name}
              isZone={isZone}
              view={view}
            />
          )}

          {!isZone &&
            continentZones.map((zone) => {
              const rect = zone.rectOnParent!
              return (
                <rect
                  key={zone.id}
                  className={zone.id === hoverZoneId ? 'zone-area hover' : 'zone-area'}
                  x={toX(rect.x)}
                  y={toY(rect.y)}
                  width={toX(rect.width)}
                  height={toY(rect.height)}
                  onMouseEnter={() => setHoverZoneId(zone.id)}
                  onMouseLeave={() => setHoverZoneId(null)}
                  onClick={() => showZone(zone.id)}
                />
              )
            })}

          {/* Every quest giver as a dot. On a continent they're small and only for looking at. */}
          <g className={isZone ? 'markers' : 'markers small'}>
            {clusters.map((cluster) => {
              const side = clusterSide(cluster)
              const radius = isZone ? Math.min(7 + cluster.markers.length * 1.5, 16) : 3.2 * unit * 1.6
              return (
                <circle
                  key={cluster.key}
                  className={`marker side-${side}${cluster.key === activeKey ? ' active' : ''}`}
                  cx={toX(cluster.x)}
                  cy={toY(cluster.y)}
                  r={radius}
                  onMouseEnter={isZone ? () => setHoverKey(cluster.key) : undefined}
                  onClick={
                    isZone
                      ? () => {
                          setPinnedKey(cluster.key)
                          onSelectQuest(cluster.markers[0].questId)
                        }
                      : undefined
                  }
                />
              )
            })}
          </g>

          {selectedMarker && (
            <circle
              className="selected-ring"
              cx={toX(selectedMarker.position.x)}
              cy={toY(selectedMarker.position.y)}
              r={isZone ? 22 : 12 * unit * 1.6}
            />
          )}

          {/* Zone names. Cities are small and lie inside a zone, so they're a diamond whose name shows on hover. */}
          {!isZone &&
            continentZones.map((zone) => {
              const rect = zone.rectOnParent!
              const count = countByZone.get(zone.id) ?? 0
              const isCity = rect.width < 5
              const hovered = zone.id === hoverZoneId
              const cx = toX(rect.x + rect.width / 2)
              const cy = toY(rect.y + rect.height / 2)
              const size = isCity ? labelSize * 0.85 : labelSize
              const label = `${zone.name}${count ? `  ${count}` : ''}`
              const width = label.length * size * 0.55 + size * 0.9
              return (
                <g
                  key={zone.id}
                  className={`zone-label${hovered ? ' hover' : ''}${isCity ? ' city' : ''}`}
                  transform={`translate(${cx}, ${cy})`}
                  onMouseEnter={() => setHoverZoneId(zone.id)}
                  onMouseLeave={() => setHoverZoneId(null)}
                  onClick={() => showZone(zone.id)}
                >
                  {isCity && (
                    <rect
                      className="city-mark"
                      x={-size * 0.4}
                      y={-size * 0.4}
                      width={size * 0.8}
                      height={size * 0.8}
                      transform="rotate(45)"
                    />
                  )}
                  {(!isCity || hovered) && (
                    <g transform={isCity ? `translate(0, ${-size * 1.5})` : undefined}>
                      <rect x={-width / 2} y={-size * 0.75} width={width} height={size * 1.5} rx={size * 0.35} />
                      <text textAnchor="middle" dy={size * 0.35} fontSize={size}>
                        {zone.name}
                        {count > 0 && <tspan className="zone-count">{`  ${count}`}</tspan>}
                      </text>
                    </g>
                  )}
                </g>
              )
            })}
        </svg>

        {activeCluster && (
          <div
            className={activeCluster.x > 60 ? 'map-popup left' : 'map-popup'}
            style={{ left: `${activeCluster.x}%`, top: `${activeCluster.y}%` }}
            onMouseEnter={() => setHoverKey(activeCluster.key)}
          >
            <p className="map-popup-npc">{activeCluster.markers[0].npcName}</p>
            <ul>
              {activeCluster.markers.map((marker) => (
                <li key={marker.questId}>
                  <button
                    type="button"
                    className={marker.questId === selectedQuestId ? 'selected' : undefined}
                    onClick={() => {
                      setPinnedKey(activeCluster.key)
                      onSelectQuest(marker.questId)
                    }}
                  >
                    <span className={`level side-${marker.side}`}>{marker.level ?? '–'}</span>
                    {marker.title}
                    <span className="map-popup-audio">
                      {marker.partsWithAudio}/{marker.parts}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
            {pinnedKey && (
              <button type="button" className="map-popup-close" onClick={() => setPinnedKey(null)}>
                Close
              </button>
            )}
          </div>
        )}
      </div>

      <div className="map-legend">
        <span>
          <i className="swatch side-alliance" /> Alliance
        </span>
        <span>
          <i className="swatch side-horde" /> Horde
        </span>
        <span>
          <i className="swatch side-both" /> Both factions
        </span>
        <span className="map-hint">
          {isZone ? 'Hover a dot to see its quests, click to open one.' : 'Click a zone to see its quests.'}
          {!hasImage && ' Drawn map: run the importer’s mapimages command to get the real one.'}
        </span>
      </div>
    </div>
  )
}

// One colour per spot: Alliance or Horde when all its quests are, otherwise "both".
function clusterSide(cluster: Cluster): string {
  const sides = new Set(cluster.markers.map((marker) => marker.side))
  return sides.size === 1 ? [...sides][0] : 'both'
}

// Used until a real map image exists: a dark sea, a faint grid, and on a continent its zones as land.
interface DrawnBackgroundProps {
  rects: MapZone['rectOnParent'][]
  name: string
  isZone: boolean
  view: { x: number; y: number; width: number; height: number }
}

function DrawnBackground({ rects, name, isZone, view }: DrawnBackgroundProps) {
  return (
    <g className="drawn">
      <rect className="sea" width={WIDTH} height={HEIGHT} />
      {Array.from({ length: 14 }, (_, i) => (
        <line key={`v${i}`} className="grid" x1={(i + 1) * 100} y1={0} x2={(i + 1) * 100} y2={HEIGHT} />
      ))}
      {Array.from({ length: 9 }, (_, i) => (
        <line key={`h${i}`} className="grid" x1={0} y1={(i + 1) * 100} x2={WIDTH} y2={(i + 1) * 100} />
      ))}
      {rects.map(
        (rect, i) =>
          rect && (
            <rect
              key={i}
              className="land"
              x={toX(rect.x)}
              y={toY(rect.y)}
              width={toX(rect.width)}
              height={toY(rect.height)}
              rx={18}
            />
          ),
      )}
      {isZone && <rect className="land" x={40} y={40} width={WIDTH - 80} height={HEIGHT - 80} rx={40} />}
      <text
        className="watermark"
        x={view.x + view.width / 2}
        y={view.y + view.height - view.height * 0.05}
        fontSize={view.height * 0.064}
        textAnchor="middle"
      >
        {name}
      </text>
    </g>
  )
}
