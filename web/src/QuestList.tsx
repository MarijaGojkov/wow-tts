import type { QuestSummary } from './api'
import { audioCount, partLabels } from './labels'

interface QuestListProps {
  quests: QuestSummary[]
  selectedId: number | null
  onSelect: (id: number) => void
}

export function QuestList({ quests, selectedId, onSelect }: QuestListProps) {
  return (
    <ul className="quest-list">
      {quests.map((quest) => (
        <li key={quest.id}>
          <button
            type="button"
            className={quest.id === selectedId ? 'quest-row selected' : 'quest-row'}
            onClick={() => onSelect(quest.id)}
          >
            <span className="quest-row-title">
              <span className={`level side-${quest.side}`} title={quest.side === 'both' ? 'Both factions' : quest.side}>
                {quest.level ?? '–'}
              </span>
              {quest.title}
            </span>
            <span className="quest-row-meta">
              <span className="quest-row-where">
                {quest.npcName}
                {quest.mapZoneName && ` · ${quest.mapZoneName}`}
              </span>
              <span
                className="audio-dots"
                title={`${audioCount(quest.parts)} of ${quest.parts.length} parts have audio`}
              >
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
