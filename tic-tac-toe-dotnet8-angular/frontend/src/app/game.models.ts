export type GameMode = 'TwoPlayer' | 'Computer';
export type GameStatus = 'InProgress' | 'Won' | 'Draw';
export type Player = 'X' | 'O';

export interface MoveRecord {
  moveNumber: number;
  player: Player;
  row: number;
  column: number;
  cellIndex: number;
}

export interface GameState {
  id: string;
  board: (Player | null)[];
  currentPlayer: Player;
  mode: GameMode;
  status: GameStatus;
  winner: Player | null;
  winningCells: number[];
  moveHistory: MoveRecord[];
  resultCounted: boolean;
}

export interface Scoreboard {
  xWins: number;
  oWins: number;
  draws: number;
}
