import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { GameApiService } from './game-api.service';
import { GameMode, GameState, Scoreboard } from './game.models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  private readonly api = inject(GameApiService);

  game: GameState | null = null;
  scoreboard: Scoreboard = { xWins: 0, oWins: 0, draws: 0 };
  selectedMode: GameMode = 'TwoPlayer';
  busy = false;
  errorMessage = '';
  infoMessage = '';

  ngOnInit(): void {
    this.refreshScoreboard();
    this.newGame();
  }

  newGame(): void {
    this.busy = true;
    this.errorMessage = '';
    this.infoMessage = '';
    this.api.createGame(this.selectedMode).subscribe({
      next: game => { this.game = game; this.busy = false; },
      error: err => this.handleError(err)
    });
  }

  changeMode(mode: GameMode): void {
    if (this.busy) return;
    this.selectedMode = mode;
    this.newGame();
  }

  play(index: number): void {
    if (!this.game || this.busy || this.game.status !== 'InProgress' || this.game.board[index] !== null) return;
    this.busy = true;
    this.errorMessage = '';
    this.infoMessage = '';
    this.api.makeMove(this.game.id, this.game.currentPlayer, Math.floor(index / 3), index % 3).subscribe({
      next: game => {
        this.game = game;
        this.busy = false;
        this.refreshScoreboard();
      },
      error: err => this.handleError(err)
    });
  }

  undo(): void {
    if (!this.game || this.busy || this.game.status !== 'InProgress' || this.game.moveHistory.length === 0) return;
    this.busy = true;
    this.errorMessage = '';
    this.api.undo(this.game.id).subscribe({
      next: game => { this.game = game; this.busy = false; },
      error: err => this.handleError(err)
    });
  }

  resetGame(): void {
    if (!this.game || this.busy) return;
    this.busy = true;
    this.errorMessage = '';
    this.api.resetGame(this.game.id).subscribe({
      next: game => {
        this.game = game;
        this.selectedMode = game.mode;
        this.busy = false;
        this.infoMessage = 'A fresh game has started. The scoreboard is unchanged.';
      },
      error: err => this.handleError(err)
    });
  }

  resetScoreboard(): void {
    if (this.busy) return;
    this.busy = true;
    this.api.resetScoreboard().subscribe({
      next: score => {
        this.scoreboard = score;
        this.busy = false;
        this.infoMessage = 'Scoreboard reset.';
      },
      error: err => this.handleError(err)
    });
  }

  refreshScoreboard(): void {
    this.api.getScoreboard().subscribe({
      next: score => this.scoreboard = score,
      error: err => this.handleError(err)
    });
  }

  get statusText(): string {
    if (!this.game) return 'Starting game…';
    if (this.game.status === 'Won') return `Player ${this.game.winner} wins!`;
    if (this.game.status === 'Draw') return 'It’s a draw!';
    if (this.game.mode === 'Computer') {
      return this.game.currentPlayer === 'X' ? 'Your turn (X)' : 'Computer thinking…';
    }
    return `Player ${this.game.currentPlayer}’s turn`;
  }

  isWinningCell(index: number): boolean {
    return !!this.game?.winningCells.includes(index);
  }

  position(move: { row: number; column: number }): string {
    return `Row ${move.row + 1}, Column ${move.column + 1}`;
  }

  cellLabel(index: number, cell: string | null): string {
    return `Row ${Math.floor(index / 3) + 1}, column ${(index % 3) + 1}${cell ? ', ' + cell : ', empty'}`;
  }

  private handleError(error: unknown): void {
    this.busy = false;
    const httpError = error as HttpErrorResponse;
    this.errorMessage = httpError?.error?.message
      ?? (httpError?.status === 0
        ? 'Cannot reach the API. Start the .NET backend at https://localhost:7080.'
        : 'Something went wrong. Please try again.');
  }
}
