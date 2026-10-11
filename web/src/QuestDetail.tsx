import { useEffect, useRef, useState } from 'react'
import {
  audioUrl,
  deleteAudio,
  generateAudio,
  getEngines,
  recastVoice,
  type Quest,
  type QuestNpc,
  type QuestNpcRole,
  type AudioTimings,
  type QuestPartInfo,
  type VoiceEngine,
  type VoiceEngineInfo,
  type VoicePerformance,
} from './api'
import { audioCount, errorText, partLabels, sideLabels, sourceLabels, splitWords, voiceLabel } from './labels'

interface QuestDetailProps {
  quest: Quest
  onChanged: () => Promise<void>
}

// Bumped after each regeneration, so the browser fetches the new MP3 instead of an old one from its cache.
// Starts at the load time, so it never repeats a number used before a page reload.
let audioVersion = Date.now()

// Until the API says which engines are set up, offer Azure Speech, which always is.
const defaultEngines: VoiceEngineInfo[] = [{ id: 'azure', name: 'Azure Speech', available: true }]

export function QuestDetail({ quest, onChanged }: QuestDetailProps) {
  const [recasting, setRecasting] = useState<{ role: QuestNpcRole; engine: VoiceEngine } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [engines, setEngines] = useState<VoiceEngineInfo[]>(defaultEngines)

  useEffect(() => {
    getEngines().then(setEngines, () => setEngines(defaultEngines))
  }, [])

  const giver: QuestNpc = {
    name: quest.npcName,
    race: quest.npcRace,
    gender: quest.npcGender,
    voice: quest.voice,
    character: quest.character,
  }
  const speakers: { role: QuestNpcRole; label: string; npc: QuestNpc }[] = [
    { role: 'giver', label: 'Quest giver', npc: giver },
    ...(quest.ender ? [{ role: 'ender' as const, label: 'Hand in to', npc: quest.ender }] : []),
  ]

  async function recast(role: QuestNpcRole, engine: VoiceEngine) {
    setRecasting({ role, engine })
    setError(null)
    try {
      await recastVoice(quest.id, role, engine)
      await onChanged()
    } catch (e) {
      setError(errorText(e, 'Recasting the voice failed.'))
    } finally {
      setRecasting(null)
    }
  }

  return (
    <article className="detail">
      <header className="detail-head">
        <p className="eyebrow">Quest {quest.id}</p>
        <h2>{quest.title}</h2>
        <a
          className="wowhead"
          href={`https://www.wowhead.com/classic/quest=${quest.id}`}
          target="_blank"
          rel="noreferrer"
        >
          View on Wowhead
        </a>
      </header>

      <section className="npc" aria-label="Quest details">
        {speakers.map(({ role, label, npc }) => (
          <NpcCells
            key={role}
            label={label}
            npc={npc}
            recasting={recasting?.role === role ? recasting.engine : null}
            disabled={recasting !== null}
            onRecast={(engine) => recast(role, engine)}
          />
        ))}
        <div>
          <p className="label">Where</p>
          <p>{quest.mapZoneName ?? 'Unknown'}</p>
          {quest.mapPosition && (
            <p className="sub">
              {quest.mapPosition.x.toFixed(1)}, {quest.mapPosition.y.toFixed(1)}
            </p>
          )}
        </div>
        <div>
          <p className="label">Level</p>
          <p>{quest.level ?? 'Scales'}</p>
          {quest.minLevel !== null && <p className="sub">Requires {quest.minLevel}</p>}
        </div>
        <div>
          <p className="label">Faction</p>
          <p className={`side-text side-${quest.side}`}>{sideLabels[quest.side]}</p>
        </div>
        <div>
          <p className="label">Quest log</p>
          <p>{quest.categoryName ?? 'Unknown'}</p>
        </div>
        <div>
          <p className="label">Text from</p>
          <p>{sourceLabels[quest.source] ?? quest.source}</p>
        </div>
        <div>
          <p className="label">Audio</p>
          <p>
            {audioCount(quest.parts)} of {quest.parts.length} parts
          </p>
        </div>
      </section>

      {speakers.map(
        ({ role, npc }) =>
          npc.voice && (
            <p key={role} className="voice-reason">
              {quest.ender && `${npc.name}: `}“{npc.voice.reason}”
            </p>
          ),
      )}
      {error && <p className="error">{error}</p>}

      <div className="parts">
        {quest.parts.map((part) => (
          <PartCard
            key={`${quest.id}-${part.part}`}
            questId={quest.id}
            part={part}
            engines={engines}
            showSpeaker={quest.ender !== null}
            onChanged={onChanged}
          />
        ))}
      </div>
    </article>
  )
}

// An NPC's name, gender and race, and how they're cast (Azure voice, GPT character), each with a recast button.
interface NpcCellsProps {
  label: string
  npc: QuestNpc
  recasting: VoiceEngine | null
  disabled: boolean
  onRecast: (engine: VoiceEngine) => void
}

function NpcCells({ label, npc, recasting, disabled, onRecast }: NpcCellsProps) {
  return (
    <>
      <div>
        <p className="label">{label}</p>
        <p className="npc-name">{npc.name}</p>
        <p className="sub">
          {[npc.gender, npc.race].filter((value) => value !== 'Unknown').map(splitWords).join(' ') || 'Gender unknown'}
        </p>
      </div>
      <div className="npc-voice">
        <p className="label">Voice</p>
        {npc.voice ? (
          <>
            <p title={npc.voice.reason}>{voiceLabel(npc.voice.voice)}</p>
            <button type="button" className="link-button" onClick={() => onRecast('azure')} disabled={disabled}>
              {recasting === 'azure' ? 'Recasting…' : 'Recast'}
            </button>
          </>
        ) : (
          <p className="sub">Cast on first audio</p>
        )}
        {npc.character && (
          <div className="npc-character">
            <p title={npc.character.reason}>{voiceLabel(npc.character.voice)}</p>
            <p className="sub" title={npc.character.character}>
              {npc.character.character}
            </p>
            <button type="button" className="link-button" onClick={() => onRecast('openAi')} disabled={disabled}>
              {recasting === 'openAi' ? 'Recasting…' : 'Recast GPT'}
            </button>
          </div>
        )}
      </div>
    </>
  )
}

interface PartCardProps {
  questId: number
  part: QuestPartInfo
  engines: VoiceEngineInfo[]
  // Only worth showing when the quest has two speakers.
  showSpeaker: boolean
  onChanged: () => Promise<void>
}

function PartCard({ questId, part, engines, showSpeaker, onChanged }: PartCardProps) {
  const [busy, setBusy] = useState<VoiceEngine | null>(null)
  const [error, setError] = useState<string | null>(null)
  // Changes after regenerating, so the browser fetches the new MP3 instead of playing the old one from its cache.
  const [version, setVersion] = useState(0)
  const label = partLabels[part.part]

  async function generate(engine: VoiceEngine) {
    setBusy(engine)
    setError(null)
    try {
      if (part.hasAudio) await deleteAudio(questId, part.part)
      await generateAudio(questId, part.part, engine)
      setVersion(++audioVersion)
      await onChanged()
    } catch (e) {
      setError(errorText(e, 'Generating the audio failed.'))
    } finally {
      setBusy(null)
    }
  }

  const src = audioUrl(questId, part.part) + (version ? `?v=${version}` : '')
  const audio = useRef<HTMLAudioElement>(null)
  const playingMs = usePlaybackPosition(audio, part.hasAudio ? src : null)
  const timings = part.hasAudio ? part.timings : null

  // The audio isn't loaded until it first plays (preload="none"), and a jump before that is ignored,
  // so wait for it to load first.
  function playFrom(ms: number) {
    const element = audio.current
    if (!element) return
    const jump = () => {
      element.currentTime = ms / 1000
      void element.play()
    }
    if (element.readyState >= HTMLMediaElement.HAVE_METADATA) {
      jump()
    } else {
      element.addEventListener('loadedmetadata', jump, { once: true })
      element.load()
    }
  }

  return (
    <section className="part">
      <header className="part-head">
        <div>
          <h3>{label.name}</h3>
          <p className="part-when">
            {label.when}
            {showSpeaker && ` · ${part.speaker}`}
          </p>
        </div>
        <span className={part.hasAudio ? 'status ready' : 'status missing'}>
          {part.hasAudio ? 'Audio ready' : 'No audio yet'}
        </span>
      </header>

      {timings ? (
        <KaraokeText text={part.text} timings={timings} playingMs={playingMs} onWordClick={playFrom} />
      ) : (
        <p className="part-text">{part.text}</p>
      )}

      {part.hasAudio && <PerformanceLine performance={part.performance} />}

      <footer className="part-foot">
        {part.hasAudio && <audio ref={audio} controls preload="none" src={src} />}
        {engines.map((engine) => (
          <button
            key={engine.id}
            type="button"
            className={part.hasAudio ? 'regenerate' : 'generate'}
            onClick={() => generate(engine.id)}
            disabled={busy !== null || !engine.available}
            title={engine.available ? undefined : `${engine.name} isn't set up yet: see "Two voice engines" in the README`}
          >
            {busy === engine.id ? 'Directing and generating…' : `${part.hasAudio ? 'Regenerate' : 'Generate'} · ${engine.name}`}
          </button>
        ))}
        {timings && <TimingBadge timings={timings} />}
        <span
          className="chars"
          title="Counted the way Azure bills text to speech: every letter, space, punctuation mark and line break"
        >
          {part.text.length} characters incl. spaces
        </span>
      </footer>

      {error && <p className="error">{error}</p>}
    </section>
  )
}

// Where the audio is, in milliseconds, while it plays (null when it isn't playing). Checked every frame,
// because the audio element's own timeupdate event only fires a few times a second, too slow for karaoke.
// timeupdate still helps where frames are paused, e.g. in a background tab.
// src makes it attach again when the audio element appears or changes (after generating or regenerating).
function usePlaybackPosition(audio: React.RefObject<HTMLAudioElement | null>, src: string | null): number | null {
  const [position, setPosition] = useState<number | null>(null)

  useEffect(() => {
    const element = audio.current
    if (!element) return
    let frame = 0
    const tick = () => {
      setPosition(element.currentTime * 1000)
      frame = requestAnimationFrame(tick)
    }
    const start = () => {
      cancelAnimationFrame(frame)
      frame = requestAnimationFrame(tick)
    }
    const stop = () => {
      cancelAnimationFrame(frame)
      setPosition(element.ended ? null : element.currentTime * 1000)
    }
    const update = () => setPosition(element.currentTime * 1000)
    element.addEventListener('play', start)
    element.addEventListener('pause', stop)
    element.addEventListener('ended', stop)
    element.addEventListener('seeked', update)
    element.addEventListener('timeupdate', update)
    return () => {
      cancelAnimationFrame(frame)
      element.removeEventListener('play', start)
      element.removeEventListener('pause', stop)
      element.removeEventListener('ended', stop)
      element.removeEventListener('seeked', update)
      element.removeEventListener('timeupdate', update)
    }
  }, [audio, src])

  return position
}

// The part's text with each word as its own span: words already spoken are dimmed, the one being spoken is lit.
// Clicking a word plays from there.
function KaraokeText({
  text,
  timings,
  playingMs,
  onWordClick,
}: {
  text: string
  timings: AudioTimings
  playingMs: number | null
  onWordClick: (ms: number) => void
}) {
  const pieces: React.ReactNode[] = []
  let position = 0
  timings.words.forEach((word, index) => {
    if (word.offset > position) pieces.push(text.slice(position, word.offset))
    const next = timings.words[index + 1]
    const current = playingMs !== null && playingMs >= word.startMs && (!next || playingMs < next.startMs)
    const spoken = playingMs !== null && !current && playingMs >= word.startMs
    pieces.push(
      <span
        key={word.offset}
        className={current ? 'word word-current' : spoken ? 'word word-spoken' : 'word'}
        onClick={() => onWordClick(word.startMs)}
      >
        {text.slice(word.offset, word.offset + word.length)}
      </span>,
    )
    position = word.offset + word.length
  })
  if (position < text.length) pieces.push(text.slice(position))

  return <p className="part-text karaoke">{pieces}</p>
}

// "Karaoke · Whisper 96%": where the word timings came from, and how much of the text the source confirmed.
function TimingBadge({ timings }: { timings: AudioTimings }) {
  const source = timings.source === 'azure' ? 'Azure' : 'Whisper'
  const title =
    timings.source === 'azure'
      ? 'Timings reported by Azure Speech while it spoke the line'
      : 'Timings from Whisper listening to the audio, lined up with the text'
  return (
    <span className={timings.match < 0.7 ? 'timing-badge poor' : 'timing-badge'} title={title}>
      Karaoke · {source} {Math.round(timings.match * 100)}%
    </span>
  )
}

// "Davis (US) · angry ×1.3 · slower 10% · lower 15%", and the director's reason underneath.
function PerformanceLine({ performance }: { performance: VoicePerformance | null }) {
  if (!performance) {
    return <p className="performance muted">Made before the voice director, so how it was spoken wasn't recorded.</p>
  }

  const badges = { ai: 'AI', default: 'Default', imported: 'Imported' }
  if (performance.directedBy === 'imported') {
    return (
      <div className="performance">
        <p>
          <span className="director director-imported">{badges.imported}</span>
          {performance.voice}
        </p>
        <p className="performance-reason">{performance.reason}</p>
      </div>
    )
  }

  if (performance.engine === 'openAi') {
    return (
      <div className="performance">
        <p>
          <span className={`director director-${performance.directedBy}`}>{badges[performance.directedBy]}</span>
          {voiceLabel(performance.voice)}
        </p>
        {performance.instructions && <p className="performance-instructions">{performance.instructions}</p>}
        <p className="performance-reason">{performance.reason}</p>
      </div>
    )
  }

  const details = [
    voiceLabel(performance.voice),
    performance.style
      ? performance.styleDegree === 1 ? performance.style : `${performance.style} ×${performance.styleDegree}`
      : 'no style',
    performance.ratePercent !== 0 && `${performance.ratePercent < 0 ? 'slower' : 'faster'} ${Math.abs(performance.ratePercent)}%`,
    performance.pitchPercent !== 0 && `${performance.pitchPercent < 0 ? 'lower' : 'higher'} ${Math.abs(performance.pitchPercent)}%`,
  ].filter(Boolean)

  return (
    <div className="performance">
      <p>
        <span className={`director director-${performance.directedBy}`}>
          {badges[performance.directedBy]}
        </span>
        {details.join(' · ')}
      </p>
      <p className="performance-reason">{performance.reason}</p>
    </div>
  )
}
