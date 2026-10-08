import type { Quest } from './api'
import { audioCount, partLabels, splitWords } from './labels'

interface QuestListProps {
  quests: Quest[]
  selectedId: number | null
  onSelect: (id: number) => void
}

export function QuestList({ quests, selectedId, onSelect }: QuestListProps) {
  if (quests.length === 0) {
    return <p className="empty">No quests match.</p>
  }

  return (
    <ul className="quest-list">
      {quests.map((quest) => (
        <li key={quest.id}>
          <button
            type="button"
            className={quest.id === selectedId ? 'quest-row selected' : 'quest-row'}
            onClick={() => onSelect(quest.id)}
          >
            <span className="quest-row-title">{quest.title}</span>
            <span className="quest-row-meta">
              <span>
                {quest.npcName} · {splitWords(quest.npcRace)}
              </span>
              <span className="audio-dots" title={`${audioCount(quest)} of ${quest.parts.length} parts have audio`}>
                {quest.parts.map((part) => (
                  <span
                    key={part.part}
                    className={part.hasAudio ? 'dot on' : 'dot'}
                    aria-label={`${partLabels[part.part].name}: ${part.hasAudio ? 'audio ready' : 'no audio'}`}
                  />
                ))}
              </span>
            </span>
          </button>
        </li>
      ))}
    </ul>
  )
}
