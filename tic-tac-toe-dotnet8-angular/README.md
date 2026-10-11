# Tic Tac Toe — Angular + .NET 8

## 1. Project overview

A browser-based Tic Tac Toe game with an Angular frontend and a .NET 8 REST API. Players can play locally against each other (Two Player mode) or against a computer opponent (Computer mode).

The API is the single source of truth: it owns the board, turn order, move validation, win/draw detection, move history, computer moves, undo, and the session scoreboard. The Angular app renders whatever state the API returns and sends user actions back as REST calls.

## 2. Tech stack

| Area | Technology |
|---|---|
| Backend | .NET 8, ASP.NET Core Web API (controllers), C# |
| Frontend | Angular 18 (standalone components), TypeScript, RxJS |
| API | REST / JSON over HTTPS |
| API docs | Swashbuckle (OpenAPI / Swagger UI) |
| Storage | In-memory (lifetime of the API process) |
| Backend tests | xUnit, Microsoft.NET.Test.Sdk, Microsoft.AspNetCore.Mvc.Testing (`WebApplicationFactory`) |
| Frontend tests | Jasmine + Karma (Angular CLI default), headless Chrome |

## 3. Features implemented

- **New game** in either mode; X always moves first.
- **Two Player mode:** X and O alternate on the same device.
- **Computer mode:** the human plays X; the computer (O) replies immediately using the priority *win → block → center → corner → any free cell*.
- **Turn enforcement and move validation** on the server: out-of-range cells, occupied cells, wrong player, and moves after the game ends are rejected with a clear message shown in the UI.
- **Win detection** for all rows, columns, and diagonals; the **winning cells are highlighted**.
- **Draw detection** when the board is full with no winner.
- **Move history** showing move number, player, and position (row/column).
- **Undo by mode:** one move in Two Player mode; the human + computer move pair in Computer mode. Undo is disabled once a game is finished.
- **Scoreboard** of X wins, O wins, and draws for the session; each result is counted exactly once.
- **Reset Game** starts a fresh board in the same mode and keeps the scoreboard.
- **Reset Scoreboard** sets all totals to zero without touching the current game.
- **Swagger UI** to explore and call every endpoint manually.
- **HTTPS** for both the API and the Angular dev server.
- **Automated tests:** backend unit + HTTP integration tests, Angular service + component tests.
- Responsive UI.

## 4. How to run the backend locally

### Prerequisites

- .NET 8 SDK (a newer SDK with the .NET 8 runtime installed also works)
- A trusted ASP.NET Core HTTPS development certificate (one-time setup):

  ```bash
  dotnet dev-certs https --trust
  ```

### Run

From the repository root:

```bash
cd backend/TicTacToe.Api
dotnet restore
dotnet run
```

`dotnet run` (or F5 in Visual Studio) uses the `TicTacToe.Api` launch profile:

- **HTTPS:** `https://localhost:7080` (used by the Angular app)
- **HTTP:** `http://localhost:5080`, which redirects to HTTPS (307)

### Swagger UI

With the API running, open **`https://localhost:7080/swagger`** (it opens automatically on launch).

- Every endpoint is listed with request/response schemas and possible status codes (200, 400, 404, 409).
- Use **Try it out → Execute** to call the live API; request bodies are pre-filled with valid examples.
- Typical manual flow:
  1. `POST /api/games` with `{"mode":"TwoPlayer"}` and copy the returned `id`.
  2. `POST /api/games/{id}/moves` with `{"player":"X","row":0,"column":0}`, then O, and so on.
  3. Try invalid moves (occupied cell, wrong player, `row: 3`) to see 400/409 responses with a `message`.
  4. Try `undo`, `reset`, `GET /api/scoreboard`, and `POST /api/scoreboard/reset`.
- The raw OpenAPI document is at `https://localhost:7080/swagger/v1/swagger.json`.
- Swagger is enabled only in the Development environment (set by the launch profile).

## 5. How to run the frontend locally

### Prerequisites

- Node.js 20.11+ and npm
- The backend running on `https://localhost:7080` (see above)

### Run

In a second terminal, from the repository root:

```bash
cd frontend
npm install
npm start
```

Open **`https://localhost:4200`**.

- The dev server runs over HTTPS. Before each `npm start`, a `prestart` script (`scripts/ensure-dev-cert.mjs`) exports the trusted .NET development certificate to `frontend/.certs/` if it is not already there. That folder contains a private key and is git-ignored. If the certificate is renewed, delete `frontend/.certs/` and run `npm start` again.
- The API base URL is configured in `src/environments/environment.ts`.
- If the UI shows *"Cannot reach the API"*, check that the backend is running on `https://localhost:7080`.

## 6. API endpoint summary

All request and response bodies are JSON. Rows and columns are zero-based (`0`–`2`); the UI displays them as 1–3.

| Method | Endpoint | Purpose | Success | Errors |
|---|---|---|---|---|
| `POST` | `/api/games` | Create a game. Body: `{"mode":"TwoPlayer"}` or `{"mode":"Computer"}` | 200 | 400 invalid mode |
| `GET` | `/api/games/{id}` | Get current game state | 200 | 404 |
| `POST` | `/api/games/{id}/moves` | Submit a move. Body: `{"player":"X","row":0,"column":0}`. In Computer mode the computer's reply is included in the response | 200 | 400 bad coordinates/player, 404, 409 occupied cell / wrong turn / game over |
| `POST` | `/api/games/{id}/undo` | Undo according to the game mode | 200 | 404, 409 no moves / game over |
| `POST` | `/api/games/{id}/reset` | Start a fresh game (new id) with the same mode | 200 | 404 |
| `GET` | `/api/scoreboard` | Get session scoreboard | 200 | — |
| `POST` | `/api/scoreboard/reset` | Reset scoreboard to zero | 200 | — |

### Game state response

```json
{
  "id": "game-guid",
  "board": ["X", null, null, null, "O", null, null, null, null],
  "currentPlayer": "X",
  "mode": "TwoPlayer",
  "status": "InProgress",
  "winner": null,
  "winningCells": [],
  "moveHistory": [
    { "moveNumber": 1, "player": "X", "row": 0, "column": 0, "cellIndex": 0 }
  ]
}
```

`status` is `InProgress`, `Won`, or `Draw`. `board` is a flat nine-element array indexed row-major (0–8); empty cells are `null`.

### Scoreboard response

```json
{ "xWins": 2, "oWins": 1, "draws": 0 }
```

### Errors

Every 4xx response has the body `{ "message": "<human-readable reason>" }`, which the UI displays directly.

## 7. How to run tests

### Backend (unit + HTTP integration; no running server needed)

```bash
dotnet test backend/TicTacToe.Tests/TicTacToe.Tests.csproj
```

### Frontend (requires Google Chrome; runs headless)

```bash
cd frontend
npm install
npm run test:ci   # single headless run
npm test          # watch mode with the Karma browser UI
```

### What the tests cover

| Suite | File | Coverage |
|---|---|---|
| Game rules (unit) | `backend/TicTacToe.Tests/GameServiceTests.cs` | Valid/invalid moves, row/column/diagonal win, draw, reset, undo by mode, computer move priorities, move after completion, scoreboard |
| Game rules (unit) | `backend/TicTacToe.Tests/GameServiceAcceptanceTests.cs` | Each rule in depth: all 8 win lines, O wins, turn order, invalid input, computer mode, move history, undo by mode, scoreboard across games, both resets |
| REST API (integration) | `backend/TicTacToe.Tests/GamesApiTests.cs` | Full ASP.NET Core pipeline in memory: routes, JSON contract, 200/400/404/409 status codes, HTTP→HTTPS redirect, CORS for the Angular app, Swagger document |
| Angular API client | `frontend/src/app/game-api.service.spec.ts` | URL, HTTP verb, and body of every REST call; error propagation |
| Angular UI | `frontend/src/app/app.component.spec.ts` | Rendered page against a stubbed API: board, turns, disabled cells, winning-cell highlight, draw, history, undo, scoreboard, resets, error messages |

### Testing expectations → tests

| Expectation | Example tests |
|---|---|
| Valid move | `ValidMovePlacesMarkAndSwitchesTurn`, `ValidMovesAlternateTurns` |
| Invalid move | `RejectsOccupiedCellAndPreservesTurn`, `RejectsCoordinatesOutsideTheBoard`, `RejectsUnknownPlayerSymbol`, `OccupiedCellReturns409` |
| Turn switching | `TurnsAlternateBetweenXAndOInTwoPlayerMode`, `RejectsMoveFromPlayerOutOfTurnAndLeavesBoardUnchanged` |
| Row / column / diagonal win | `DetectsRowWins`, `DetectsColumnWin`, `DetectsDiagonalWin`, `DetectsEveryRowColumnAndDiagonalWinWithWinningCells` |
| Draw | `DetectsDrawAndCountsItOnce`, `DrawHasNoWinnerAndNoWinningCells` |
| Reset game | `ResetCreatesFreshBoardAndKeepsScore`, `ResetGameKeepsModeAndStartsFreshMidGame` |
| Undo (two-player) | `UndoTwoPlayerRemovesOnlyLastMove`, `TwoPlayerUndoCanRewindToEmptyBoardOneMoveAtATime` |
| Undo (computer) | `UndoComputerModeRemovesHumanAndComputerPair`, `ComputerUndoRemovesOnlyTheLatestPair` |
| Scoreboard update | `ScoreboardAccumulatesAcrossGames`, `ComputerCanWinAndScoresForO`, `ScoreboardCanBeReset` |
| Computer move selection | `ComputerTakesWinningMoveBeforeOtherPriorities`, `ComputerBlocksXBeforeTakingCenter`, `ComputerChoosesCenterThenCorner`, `ComputerFallsBackToFirstFreeCellWhenCenterAndCornersAreTaken` |
| Move after completion | `RejectsMoveAfterCompletion`, `MoveAfterGameOverReturns409` |
| Component rendering / API integration | `app.component.spec.ts`, `game-api.service.spec.ts` |

### Manual end-to-end check

1. Start the API and the Angular app; open `https://localhost:4200`.
2. **Two Players:** play X top-left, O center; the status alternates and history lists both moves. *Undo move* removes only O's move.
3. Play X across the top row: the three cells highlight, *Player X wins!* is shown, X wins increments, Undo is disabled.
4. Click *New game*: the board clears and the scoreboard is unchanged.
5. **Play Computer:** click a corner; the computer answers in the center. *Undo move* removes both moves.
6. Play a drawn game: *It's a draw!* is shown and Draws increments.
7. Click *Reset scoreboard*: all totals return to 0.

## 8. AI tools and prompt summary

AI tools were used to speed up development. All generated code was reviewed, run locally, and verified with automated tests.

| Tool | Used for |
|---|---|
| **ChatGPT** | Initial build: breaking the assignment into requirements, the .NET API (game service, controllers, models), the Angular UI, the first set of xUnit tests, and the first README draft |
| **Claude Code** (VS Code extension) | Improvements after the initial build: extending the test suites, adding Angular tests, Swagger, HTTPS, and README restructuring |

### Prompt summary — initial build (ChatGPT)

- Convert the assignment into a checklist of API contracts, game rules, edge cases, and tests.
- Implement a .NET 8 game service where the backend owns board state, validates turns, detects wins/draws, and counts each result on the scoreboard exactly once.
- Implement the computer move priority: win, block, center, corner, any available cell.
- Create xUnit tests for valid/invalid moves, win conditions, draw, reset, undo by mode, scoreboard, and computer move selection.
- Build an Angular standalone UI that calls the REST API and renders the server-returned state.
- Review the generated code for correct state transitions, duplicate-result risks, and UI/API contract consistency.

### Prompt summary — improvements (Claude Code)

- Add test cases so that every acceptance criterion is covered. This added backend acceptance tests, HTTP integration tests with `WebApplicationFactory`, and Angular tests (Karma/Jasmine setup, API service spec, component spec).
- Check that the listed testing expectations (valid/invalid move, turn switching, wins, draw, reset, undo by mode, scoreboard, computer move, move after completion) are all covered.
- Integrate Swagger into the backend so all endpoints can be viewed and tested manually.
- Keep HTTPS enabled for the API, and run the Angular dev server over HTTPS using the trusted .NET development certificate.
- Check the README against the required sections and restructure it.

### How AI output was verified

- Backend and frontend test suites were run locally after every change.
- A deliberate bug was introduced (winning-cell highlight disabled) to confirm the frontend tests fail when behavior breaks.
- Swagger, HTTPS, the HTTP→HTTPS redirect, and CORS were checked against a running API instance.

## 9. Design decisions

1. **Backend is the source of truth.** All rules and state transitions live in `GameService`; the Angular app never decides whether a move is valid or who won. This keeps the rules in one place and makes them easy to unit test.
2. **Single in-memory service with a lock.** `GameService` is registered as a singleton holding games in a dictionary. A lock makes each operation atomic, so concurrent requests cannot corrupt a board or double-count a result.
3. **Defensive copies.** The service returns copies of game state, so callers cannot mutate stored games by accident.
4. **Computer moves in the same request.** In Computer mode the API applies the human move and the computer's reply in one call, so the client never sees a half-finished turn and needs no polling.
5. **Results counted exactly once.** A `ResultCounted` flag on each game ensures a win or draw updates the scoreboard once, however many times the state is read.
6. **Reset creates a new game id.** Resetting starts a brand-new game in the same mode rather than mutating the old one, which keeps each game's history self-contained.
7. **Consistent error mapping.** Validation errors map to HTTP status codes: 400 for malformed input, 404 for unknown game, 409 for moves that conflict with the game state. Every error has a `{ "message" }` body the UI displays directly.
8. **Thin Angular layer.** One standalone `AppComponent` renders the state, and `GameApiService` wraps every REST call. A `busy` flag disables the board and buttons while a request is in flight, preventing double submissions.
9. **Secure by default locally.** The API serves HTTPS with HTTP→HTTPS redirect (HSTS outside Development), and the Angular dev server also uses HTTPS. CORS only allows the Angular dev origin.
10. **Swagger in Development only.** Interactive API docs are available for local review but are not exposed in a production environment.
11. **Layered tests.** Unit tests cover game rules, integration tests cover the HTTP contract through the real ASP.NET Core pipeline, and Angular tests cover rendering and API calls with the backend stubbed.

## 10. Clarifications and assumptions

1. **Undo after completion (Option A):** undo is disabled once a game is won or drawn, so the scoreboard stays final for that game.
2. **Computer mode roles:** the human is always X and moves first; the computer is always O.
3. **Undo in Computer mode:** removes the computer's last move and the human move before it, returning the turn to the human. (Because the computer replies in the same request, the board is never left waiting for the computer. The service still handles that case defensively by removing only the human move.)
4. **Computer strategy:** follows the specified priority exactly. Corners are tried in the order top-left, top-right, bottom-left, bottom-right, and the fallback is the first free cell. It is a heuristic, not an unbeatable (minimax) opponent.
5. **Scoreboard scope:** one scoreboard for the whole API process (the "session"), shared by all browser tabs. Resetting a game never changes scores.
6. **Switching mode:** selecting a different mode starts a new game immediately; an unfinished game is abandoned and not scored.
7. **Coordinates:** the API uses zero-based row/column (0–2); the UI shows 1–3.
8. **Storage:** in-memory storage is acceptable for this exercise; data is lost when the API restarts.
9. **Out of scope:** authentication, user accounts, and persistent storage.
10. **Environment:** the solution targets local development on `localhost` (API on ports 7080/5080, UI on 4200).

## 11. Known limitations

- Game state and scores are lost when the API restarts.
- State is held in a single process; running multiple API instances would give each its own games and scoreboard.
- The scoreboard is shared by everyone using the same API instance, not per player.
- Finished and reset games are kept in memory for the life of the process (no cleanup).
- The computer opponent is beatable by design (heuristic, not minimax).
- HTTPS relies on the local ASP.NET Core development certificate; the API URL is a single development setting with no production configuration.
- Frontend tests require Google Chrome.
- There are no browser end-to-end tests that drive the real UI against the real API.

## 12. Future improvements

- Persist games and scores (e.g. SQLite with EF Core) and add per-user scoreboards with authentication.
- Expire or clean up old games from memory.
- Add a difficulty setting with an unbeatable minimax computer.
- Add Playwright end-to-end tests running the UI against a live API.
- Add a CI pipeline (e.g. GitHub Actions) running backend and frontend tests on every push.
- Add production environment configuration, Docker / docker-compose for one-command startup, structured logging, and rate limiting.

## Project structure

```text
backend/
  TicTacToe.Api/
    Controllers/          GamesController, ScoreboardController
    Models/               Game state, requests, scoreboard, error response
    Services/             GameService (all game rules)
    Properties/           launchSettings.json (HTTPS 7080 / HTTP 5080)
    Program.cs            DI, CORS, HTTPS, Swagger
  TicTacToe.Tests/
    GameServiceTests.cs
    GameServiceAcceptanceTests.cs
    GamesApiTests.cs
frontend/
  scripts/ensure-dev-cert.mjs   Exports the HTTPS dev certificate for ng serve
  src/app/
    app.component.*             UI
    game-api.service.ts         REST client
    game.models.ts              Shared types
    *.spec.ts                   Angular tests
  src/environments/             API base URL
```
