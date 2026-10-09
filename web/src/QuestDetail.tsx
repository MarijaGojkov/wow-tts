import { useState } from 'react'
import {
  audioUrl,
  deleteAudio,
  generateAudio,
  recastVoice,
  type Quest,
  type QuestPartInfo,
  type VoicePerformance,
} from './api'
import { audioCount, errorText, partLabels, sideLabels, sourceLabels, splitWords, voiceLabel } from './labels'

interface QuestDetailProps {
  quest: Quest
  onChanged: () => Promise<void>
}

export function QuestDetail({ quest, onChanged }: QuestDetailProps) {
  const [recasting, setRecasting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function recast() {
    setRecasting(true)
    setError(null)
    try {
      await recastVoice(quest.id)
      await onChanged()
    } catch (e) {
      setError(errorText(e, 'Recasting the voice failed.'))
    } finally {
      setRecasting(false)
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
        <div>
          <p className="label">Quest giver</p>
          <p className="npc-name">{quest.npcName}</p>
          <p className="sub">
            {[quest.npcGender, quest.npcRace].filter((value) => value !== 'Unknown').map(splitWords).join(' ') ||
              'Gender unknown'}
          </p>
        </div>
        <div className="npc-voice">
          <p className="label">Voice</p>
          {quest.voice ? (
            <>
              <p title={quest.voice.reason}>{voiceLabel(quest.voice.voice)}</p>
              <button type="button" className="link-button" onClick={recast} disabled={recasting}>
                {recasting ? 'Recasting…' : 'Recast'}
              </button>
            </>
          ) : (
            <p className="sub">Cast on first audio</p>
          )}
        </div>
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

      {quest.voice && <p className="voice-reason">“{quest.voice.reason}”</p>}
      {error && <p className="error">{error}</p>}

      <div className="parts">
        {quest.parts.map((part) => (
          <PartCard key={`${quest.id}-${part.part}`} questId={quest.id} part={part} onChanged={onChanged} />
        ))}
      </div>
    </article>
  )
}

interface PartCardProps {
  questId: number
  part: QuestPartInfo
  onChanged: () => Promise<void>
}

function PartCard({ questId, part, onChanged }: PartCardProps) {
  const [busy, setBusy] = useState<'generating' | 'regenerating' | null>(null)
  const [error, setError] = useState<string | null>(null)
  // Changes after regenerating, so the browser fetches the new MP3 instead of playing the old one from its cache.
  const [version, setVersion] = useState(0)
  const label = partLabels[part.part]

  async function generate(regenerate: boolean) {
    setBusy(regenerate ? 'regenerating' : 'generating')
    setError(null)
    try {
      if (regenerate) await deleteAudio(questId, part.part)
      await generateAudio(questId, part.part)
      setVersion(Date.now())
      await onChanged()
    } catch (e) {
      setError(errorText(e, 'Generating the audio failed.'))
    } finally {
      setBusy(null)
    }
  }

  const src = audioUrl(questId, part.part) + (version ? `?v=${version}` : '')

  return (
    <section className="part">
      <header className="part-head">
        <div>
          <h3>{label.name}</h3>
          <p className="part-when">{label.when}</p>
        </div>
        <span className={part.hasAudio ? 'status ready' : 'status missing'}>
          {part.hasAudio ? 'Audio ready' : 'No audio yet'}
        </span>
      </header>

      <p className="part-text">{part.text}</p>

      {part.hasAudio && <PerformanceLine performance={part.performance} />}

      <footer className="part-foot">
        {part.hasAudio ? (
          <>
            <audio controls preload="none" src={src} />
            <button type="button" className="regenerate" onClick={() => generate(true)} disabled={busy !== null}>
              {busy === 'regenerating' ? 'Directing and generating…' : 'Regenerate'}
            </button>
          </>
        ) : (
          <button type="button" className="generate" onClick={() => generate(false)} disabled={busy !== null}>
            {busy === 'generating' ? 'Directing and generating…' : 'Generate audio'}
          </button>
        )}
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

// "Davis (US) · angry ×1.3 · slower 10% · lower 15%", and the director's reason underneath.
function PerformanceLine({ performance }: { performance: VoicePerformance | null }) {
  if (!performance) {
    return <p className="performance muted">Made before the voice director, so how it was spoken wasn't recorded.</p>
  }

  const details = [
    voiceLabel(performance.voice),
    performance.style ? `${performance.style} ×${performance.styleDegree}` : 'no style',
    performance.ratePercent !== 0 && `${performance.ratePercent < 0 ? 'slower' : 'faster'} ${Math.abs(performance.ratePercent)}%`,
    performance.pitchPercent !== 0 && `${performance.pitchPercent < 0 ? 'lower' : 'higher'} ${Math.abs(performance.pitchPercent)}%`,
  ].filter(Boolean)

  return (
    <div className="performance">
      <p>
        <span className={`director director-${performance.directedBy}`}>
          {performance.directedBy === 'ai' ? 'AI' : 'Default'}
        </span>
        {details.join(' · ')}
      </p>
      <p className="performance-reason">{performance.reason}</p>
    </div>
  )
}
