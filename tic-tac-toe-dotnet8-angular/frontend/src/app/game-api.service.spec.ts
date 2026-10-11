import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { GameApiService } from './game-api.service';
import { GameState, Scoreboard } from './game.models';
import { environment } from '../environments/environment';

describe('GameApiService', () => {
  const base = environment.apiBaseUrl;
  let service: GameApiService;
  let http: HttpTestingController;

  const game: GameState = {
    id: 'game-1',
    board: [null, null, null, null, null, null, null, null, null],
    currentPlayer: 'X',
    mode: 'TwoPlayer',
    status: 'InProgress',
    winner: null,
    winningCells: [],
    moveHistory: [],
    resultCounted: false
  };
  const score: Scoreboard = { xWins: 1, oWins: 2, draws: 3 };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(GameApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('targets the local .NET API', () => {
    expect(base).toBe('https://localhost:7080/api');
  });

  it('createGame POSTs the selected mode to /games', () => {
    let result: GameState | undefined;
    service.createGame('Computer').subscribe(g => result = g);

    const req = http.expectOne(`${base}/games`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ mode: 'Computer' });
    req.flush(game);
    expect(result).toEqual(game);
  });

  it('getGame GETs /games/{id}', () => {
    service.getGame('game-1').subscribe();
    const req = http.expectOne(`${base}/games/game-1`);
    expect(req.request.method).toBe('GET');
    req.flush(game);
  });

  it('makeMove POSTs player, row and column to /games/{id}/moves', () => {
    service.makeMove('game-1', 'O', 2, 1).subscribe();
    const req = http.expectOne(`${base}/games/game-1/moves`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ player: 'O', row: 2, column: 1 });
    req.flush(game);
  });

  it('undo POSTs to /games/{id}/undo', () => {
    service.undo('game-1').subscribe();
    const req = http.expectOne(`${base}/games/game-1/undo`);
    expect(req.request.method).toBe('POST');
    req.flush(game);
  });

  it('resetGame POSTs to /games/{id}/reset', () => {
    service.resetGame('game-1').subscribe();
    const req = http.expectOne(`${base}/games/game-1/reset`);
    expect(req.request.method).toBe('POST');
    req.flush(game);
  });

  it('getScoreboard GETs /scoreboard', () => {
    let result: Scoreboard | undefined;
    service.getScoreboard().subscribe(s => result = s);
    const req = http.expectOne(`${base}/scoreboard`);
    expect(req.request.method).toBe('GET');
    req.flush(score);
    expect(result).toEqual(score);
  });

  it('resetScoreboard POSTs to /scoreboard/reset', () => {
    service.resetScoreboard().subscribe();
    const req = http.expectOne(`${base}/scoreboard/reset`);
    expect(req.request.method).toBe('POST');
    req.flush({ xWins: 0, oWins: 0, draws: 0 });
  });

  it('surfaces API validation errors to the caller', () => {
    let status = 0;
    let message = '';
    service.makeMove('game-1', 'X', 0, 0).subscribe({
      error: err => { status = err.status; message = err.error.message; }
    });
    http.expectOne(`${base}/games/game-1/moves`)
      .flush({ message: 'That cell is already occupied.' }, { status: 409, statusText: 'Conflict' });
    expect(status).toBe(409);
    expect(message).toBe('That cell is already occupied.');
  });
});
