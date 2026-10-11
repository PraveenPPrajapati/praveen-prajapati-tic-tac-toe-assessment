import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { AppComponent } from './app.component';
import { GameApiService } from './game-api.service';
import { GameState, MoveRecord, Player, Scoreboard } from './game.models';

type Cell = Player | null;

function makeGame(overrides: Partial<GameState> = {}): GameState {
  return {
    id: 'abcdef12-0000-0000-0000-000000000000',
    board: [null, null, null, null, null, null, null, null, null],
    currentPlayer: 'X',
    mode: 'TwoPlayer',
    status: 'InProgress',
    winner: null,
    winningCells: [],
    moveHistory: [],
    resultCounted: false,
    ...overrides
  };
}

/** Builds board + history from a list of cell indexes played alternately X, O, X... */
function playedGame(cells: number[], overrides: Partial<GameState> = {}): GameState {
  const board: Cell[] = Array(9).fill(null);
  const moveHistory: MoveRecord[] = cells.map((cellIndex, i) => {
    const player: Player = i % 2 === 0 ? 'X' : 'O';
    board[cellIndex] = player;
    return { moveNumber: i + 1, player, row: Math.floor(cellIndex / 3), column: cellIndex % 3, cellIndex };
  });
  return makeGame({
    board,
    moveHistory,
    currentPlayer: cells.length % 2 === 0 ? 'X' : 'O',
    ...overrides
  });
}

describe('AppComponent', () => {
  let fixture: ComponentFixture<AppComponent>;
  let component: AppComponent;
  let el: HTMLElement;
  let api: jasmine.SpyObj<GameApiService>;

  const zeroScore: Scoreboard = { xWins: 0, oWins: 0, draws: 0 };

  const cells = () => Array.from(el.querySelectorAll<HTMLButtonElement>('.board .cell'));
  const button = (text: string) =>
    Array.from(el.querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent!.includes(text))!;
  const statusText = () => el.querySelector('.status-line')!.textContent!.trim();
  const scoreValues = () =>
    Array.from(el.querySelectorAll('.score-item strong')).map(s => Number(s.textContent));
  const render = () => fixture.detectChanges();

  function start(initialGame = makeGame(), score = zeroScore): void {
    api.createGame.and.returnValue(of(initialGame));
    api.getScoreboard.and.returnValue(of(score));
    fixture = TestBed.createComponent(AppComponent);
    component = fixture.componentInstance;
    el = fixture.nativeElement;
    render();
  }

  beforeEach(() => {
    api = jasmine.createSpyObj<GameApiService>('GameApiService', [
      'createGame', 'getGame', 'makeMove', 'undo', 'resetGame', 'getScoreboard', 'resetScoreboard'
    ]);
    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [{ provide: GameApiService, useValue: api }]
    });
  });

  describe('new game', () => {
    it('creates a Two Player game and loads the scoreboard on startup', () => {
      start();
      expect(api.createGame).toHaveBeenCalledOnceWith('TwoPlayer');
      expect(api.getScoreboard).toHaveBeenCalled();
      expect(cells().length).toBe(9);
      expect(cells().every(c => c.textContent!.trim() === '' && !c.disabled)).toBeTrue();
      expect(statusText()).toBe('Player X’s turn');
      expect(el.textContent).toContain('LOCAL MULTIPLAYER');
    });

    it('starts a Computer game when that mode is selected', () => {
      start();
      api.createGame.and.returnValue(of(makeGame({ mode: 'Computer' })));
      button('Play Computer').click();
      render();

      expect(api.createGame).toHaveBeenCalledWith('Computer');
      expect(component.selectedMode).toBe('Computer');
      expect(statusText()).toBe('Your turn (X)');
      expect(el.textContent).toContain('YOU vs COMPUTER');
      expect(button('Play Computer').classList).toContain('active');
    });
  });

  describe('playing moves', () => {
    it('sends the current player and zero-based row/column for the clicked cell', () => {
      start();
      api.makeMove.and.returnValue(of(playedGame([5])));
      cells()[5].click();
      render();

      expect(api.makeMove).toHaveBeenCalledOnceWith(component.game!.id, 'X', 1, 2);
      expect(cells()[5].textContent!.trim()).toBe('X');
      expect(cells()[5].classList).toContain('mark-x');
      expect(cells()[5].disabled).toBeTrue();
      expect(api.getScoreboard).toHaveBeenCalledTimes(2); // refreshed after the move
    });

    it('alternates turns X -> O -> X in Two Player mode', () => {
      start();
      api.makeMove.and.returnValue(of(playedGame([0])));
      cells()[0].click();
      render();
      expect(statusText()).toBe('Player O’s turn');

      api.makeMove.and.returnValue(of(playedGame([0, 4])));
      cells()[4].click();
      render();
      expect(api.makeMove.calls.mostRecent().args).toEqual([component.game!.id, 'O', 1, 1]);
      expect(statusText()).toBe('Player X’s turn');
      expect(cells()[4].classList).toContain('mark-o');
    });

    it('renders the computer reply returned with the human move', () => {
      start(makeGame({ mode: 'Computer' }));
      api.makeMove.and.returnValue(of(playedGame([0, 4], { mode: 'Computer' })));
      cells()[0].click();
      render();

      expect(cells()[0].textContent!.trim()).toBe('X');
      expect(cells()[4].textContent!.trim()).toBe('O');
      expect(statusText()).toBe('Your turn (X)');
    });

    it('blocks board input in Computer mode while it is O’s turn', () => {
      start(playedGame([0], { mode: 'Computer' }));
      expect(statusText()).toBe('Computer thinking…');
      expect(cells().every(c => c.disabled)).toBeTrue();
    });
  });

  describe('invalid moves', () => {
    it('ignores clicks on occupied cells', () => {
      start(playedGame([0]));
      expect(cells()[0].disabled).toBeTrue();
      component.play(0);
      expect(api.makeMove).not.toHaveBeenCalled();
    });

    it('ignores moves once the game is over', () => {
      start(playedGame([0, 3, 1, 4, 2], { status: 'Won', winner: 'X', winningCells: [0, 1, 2] }));
      component.play(8);
      expect(api.makeMove).not.toHaveBeenCalled();
    });

    it('ignores clicks while a request is in flight', () => {
      start();
      component.busy = true;
      component.play(0);
      expect(api.makeMove).not.toHaveBeenCalled();
    });

    it('shows the API error message when a move is rejected', () => {
      start();
      api.makeMove.and.returnValue(throwError(() => new HttpErrorResponse({
        status: 409, error: { message: 'It is player O\'s turn.' }
      })));
      cells()[0].click();
      render();

      expect(el.querySelector('[role="alert"]')!.textContent).toContain('It is player O\'s turn.');
      expect(component.busy).toBeFalse();
      expect(cells()[0].disabled).toBeFalse();
    });

    it('explains how to start the backend when the API is unreachable', () => {
      api.createGame.and.returnValue(throwError(() => new HttpErrorResponse({ status: 0 })));
      api.getScoreboard.and.returnValue(of(zeroScore));
      fixture = TestBed.createComponent(AppComponent);
      el = fixture.nativeElement;
      render();

      expect(el.querySelector('[role="alert"]')!.textContent).toContain('Cannot reach the API');
    });
  });

  describe('win and draw', () => {
    it('announces the winner and highlights only the winning cells', () => {
      start();
      api.makeMove.and.returnValue(of(
        playedGame([0, 3, 1, 4, 2], { status: 'Won', winner: 'X', winningCells: [0, 1, 2] })));
      api.getScoreboard.and.returnValue(of({ xWins: 1, oWins: 0, draws: 0 }));
      cells()[2].click();
      render();

      expect(statusText()).toBe('Player X wins!');
      expect(el.querySelector('.status-line')!.classList).toContain('won');
      const highlighted = cells().map((c, i) => c.classList.contains('winning') ? i : -1).filter(i => i >= 0);
      expect(highlighted).toEqual([0, 1, 2]);
      expect(cells().every(c => c.disabled)).toBeTrue();
      expect(scoreValues()).toEqual([1, 0, 0]);
    });

    it('announces a draw with no highlighted cells', () => {
      start();
      api.makeMove.and.returnValue(of(playedGame([0, 1, 2, 4, 3, 5, 7, 6, 8], { status: 'Draw' })));
      api.getScoreboard.and.returnValue(of({ xWins: 0, oWins: 0, draws: 1 }));
      cells()[8].click();
      render();

      expect(statusText()).toBe('It’s a draw!');
      expect(el.querySelector('.status-line')!.classList).toContain('draw');
      expect(el.querySelectorAll('.cell.winning').length).toBe(0);
      expect(scoreValues()).toEqual([0, 0, 1]);
    });
  });

  describe('move history', () => {
    it('shows an empty state before the first move', () => {
      start();
      expect(el.querySelector('.history-empty')).not.toBeNull();
      expect(el.querySelector('.move-count')!.textContent!.trim()).toBe('0 / 9');
    });

    it('lists each move with number, player and 1-based position', () => {
      start(playedGame([0, 7]));
      const rows = Array.from(el.querySelectorAll('.history-row')).map(r =>
        Array.from(r.querySelectorAll('span')).map(s => s.textContent!.trim()));

      expect(rows).toEqual([
        ['01', 'X', 'Row 1, Column 1'],
        ['02', 'O', 'Row 3, Column 2']
      ]);
      expect(el.querySelector('.move-count')!.textContent!.trim()).toBe('2 / 9');
      expect(el.querySelector('.history-empty')).toBeNull();
    });
  });

  describe('undo', () => {
    it('is disabled when there are no moves', () => {
      start();
      expect(button('Undo').disabled).toBeTrue();
    });

    it('calls the API and renders the rewound board in Two Player mode', () => {
      start(playedGame([0, 4]));
      api.undo.and.returnValue(of(playedGame([0])));
      button('Undo').click();
      render();

      expect(api.undo).toHaveBeenCalledOnceWith(component.game!.id);
      expect(cells()[4].textContent!.trim()).toBe('');
      expect(el.querySelectorAll('.history-row').length).toBe(1);
      expect(statusText()).toBe('Player O’s turn');
    });

    it('renders the server result that removes the human/computer pair in Computer mode', () => {
      start(playedGame([0, 4], { mode: 'Computer' }));
      api.undo.and.returnValue(of(makeGame({ mode: 'Computer' })));
      button('Undo').click();
      render();

      expect(cells().every(c => c.textContent!.trim() === '')).toBeTrue();
      expect(el.querySelector('.history-empty')).not.toBeNull();
      expect(statusText()).toBe('Your turn (X)');
    });

    it('is disabled after the game is completed', () => {
      start(playedGame([0, 3, 1, 4, 2], { status: 'Won', winner: 'X', winningCells: [0, 1, 2] }));
      expect(button('Undo').disabled).toBeTrue();
      component.undo();
      expect(api.undo).not.toHaveBeenCalled();
    });
  });

  describe('scoreboard', () => {
    it('shows X wins, O wins and draws from the API', () => {
      start(makeGame(), { xWins: 3, oWins: 2, draws: 1 });
      expect(scoreValues()).toEqual([3, 2, 1]);
    });

    it('Reset scoreboard zeroes the totals without touching the board', () => {
      start(playedGame([0, 4]), { xWins: 3, oWins: 2, draws: 1 });
      api.resetScoreboard.and.returnValue(of(zeroScore));
      button('Reset scoreboard').click();
      render();

      expect(api.resetScoreboard).toHaveBeenCalledTimes(1);
      expect(scoreValues()).toEqual([0, 0, 0]);
      expect(el.querySelectorAll('.history-row').length).toBe(2);
      expect(el.querySelector('[role="status"]')!.textContent).toContain('Scoreboard reset.');
    });
  });

  describe('reset game', () => {
    it('starts a fresh game in the same mode and keeps the scoreboard', () => {
      const current = playedGame([0, 4], { mode: 'Computer' });
      start(current, { xWins: 2, oWins: 1, draws: 0 });
      api.resetGame.and.returnValue(of(makeGame({ id: 'new-game-id-0000', mode: 'Computer' })));
      button('New game').click();
      render();

      expect(api.resetGame).toHaveBeenCalledOnceWith(current.id);
      expect(component.game!.id).toBe('new-game-id-0000');
      expect(component.selectedMode).toBe('Computer');
      expect(cells().every(c => c.textContent!.trim() === '')).toBeTrue();
      expect(el.querySelector('.history-empty')).not.toBeNull();
      expect(scoreValues()).toEqual([2, 1, 0]);
      expect(api.resetScoreboard).not.toHaveBeenCalled();
      expect(el.querySelector('[role="status"]')!.textContent).toContain('scoreboard is unchanged');
    });

    it('clears highlighted winning cells after a finished game', () => {
      start(playedGame([0, 3, 1, 4, 2], { status: 'Won', winner: 'X', winningCells: [0, 1, 2] }));
      api.resetGame.and.returnValue(of(makeGame()));
      button('New game').click();
      render();

      expect(el.querySelectorAll('.cell.winning').length).toBe(0);
      expect(statusText()).toBe('Player X’s turn');
    });
  });
});
