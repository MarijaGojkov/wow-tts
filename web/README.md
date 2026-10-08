# Quest Voices

A small React + TypeScript site (Vite) for the wow-tts API. It lists the quests, shows each quest's text and quest giver, plays the generated MP3s, and can generate missing audio.

## Run

Start the API first (`dotnet run --project src/WowTts.Api` from the repo root), then:

```powershell
npm install
npm run dev
```

Open http://localhost:5173. Vite forwards requests to `/api` to the API on http://localhost:5219, so no CORS setup is needed.

## Files

| File | What it does |
|---|---|
| `src/App.tsx` | Layout, loading, search and filters |
| `src/QuestList.tsx` | The quest list on the left |
| `src/QuestDetail.tsx` | Quest details, text and audio player for each part |
| `src/api.ts` | Types and calls for the API |
| `src/labels.ts` | Small helpers and labels |
| `src/index.css` | All styles |
