import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { GameMode, GameState, Scoreboard, Player } from './game.models';

@Injectable({ providedIn: 'root' })
export class GameApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  createGame(mode: GameMode): Observable<GameState> {
    return this.http.post<GameState>(`${this.base}/games`, { mode });
  }

  getGame(id: string): Observable<GameState> {
    return this.http.get<GameState>(`${this.base}/games/${id}`);
  }

  makeMove(id: string, player: Player, row: number, column: number): Observable<GameState> {
    return this.http.post<GameState>(`${this.base}/games/${id}/moves`, { player, row, column });
  }

  undo(id: string): Observable<GameState> {
    return this.http.post<GameState>(`${this.base}/games/${id}/undo`, {});
  }

  resetGame(id: string): Observable<GameState> {
    return this.http.post<GameState>(`${this.base}/games/${id}/reset`, {});
  }

  getScoreboard(): Observable<Scoreboard> {
    return this.http.get<Scoreboard>(`${this.base}/scoreboard`);
  }

  resetScoreboard(): Observable<Scoreboard> {
    return this.http.post<Scoreboard>(`${this.base}/scoreboard/reset`, {});
  }
}
