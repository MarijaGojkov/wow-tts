import { useState } from 'react'
import { audioUrl, generateAudio, type Quest, type QuestPartInfo } from './api'
import { errorText, partLabels, splitWords } from './labels'

interface QuestDetailProps {
  quest: Quest
  onChanged: () => Promise<void>
}

export function QuestDetail({ quest, onChanged }: QuestDetailProps) {
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

      <section className="npc" aria-label="Quest giver">
        <div>
          <p className="label">Quest giver</p>
          <p className="npc-name">{quest.npcName}</p>
        </div>
        <div>
          <p className="label">Race</p>
          <p>{splitWords(quest.npcRace)}</p>
        </div>
        <div>
          <p className="label">Gender</p>
          <p>{quest.npcGender}</p>
        </div>
        <div>
          <p className="label">Audio</p>
          <p>
            {quest.parts.filter((part) => part.hasAudio).length} of {quest.parts.length} parts
          </p>
        </div>
      </section>

      <div className="parts">
        {quest.parts.map((part) => (
          <PartCard key={`${quest.id}-${part.part}`} questId={quest.id} part={part} onGenerated={onChanged} />
        ))}
      </div>
    </article>
  )
}

interface PartCardProps {
  questId: number
  part: QuestPartInfo
  onGenerated: () => Promise<void>
}

function PartCard({ questId, part, onGenerated }: PartCardProps) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const label = partLabels[part.part]

  async function generate() {
    setBusy(true)
    setError(null)
    try {
      await generateAudio(questId, part.part)
      await onGenerated()
    } catch (e) {
      setError(errorText(e, 'Generating the audio failed.'))
    } finally {
      setBusy(false)
    }
  }

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

      <footer className="part-foot">
        {part.hasAudio ? (
          <audio controls preload="none" src={audioUrl(questId, part.part)} />
        ) : (
          <button type="button" className="generate" onClick={generate} disabled={busy}>
            {busy ? 'Generating…' : 'Generate audio'}
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
