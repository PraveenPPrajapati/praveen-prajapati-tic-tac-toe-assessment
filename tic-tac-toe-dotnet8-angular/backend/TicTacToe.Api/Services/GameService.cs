using TicTacToe.Api.Models;

namespace TicTacToe.Api.Services;

public sealed class GameService
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, GameState> _games = new();
    private readonly Scoreboard _scoreboard = new();

    private static readonly int[][] WinningLines =
    [
        [0, 1, 2], [3, 4, 5], [6, 7, 8],
        [0, 3, 6], [1, 4, 7], [2, 5, 8],
        [0, 4, 8], [2, 4, 6]
    ];

    public GameState CreateGame(string mode)
    {
        if (mode is not (GameValues.TwoPlayer or GameValues.Computer))
            throw new ArgumentException("Mode must be 'TwoPlayer' or 'Computer'.");

        lock (_gate)
        {
            var game = new GameState { Id = Guid.NewGuid(), Mode = mode };
            _games.Add(game.Id, game);
            return game.Copy();
        }
    }

    public GameState GetGame(Guid id)
    {
        lock (_gate)
            return GetRequiredGame(id).Copy();
    }

    public GameState MakeMove(Guid id, string player, int row, int column)
    {
        lock (_gate)
        {
            var game = GetRequiredGame(id);
            ValidateMove(game, player, row, column);
            ApplyMove(game, player, row, column);

            if (game.Mode == GameValues.Computer && game.Status == GameValues.InProgress)
            {
                // The only human-controlled player in computer mode is X.
                if (player != GameValues.X)
                    throw new InvalidOperationException("Only player X can submit moves in Computer mode.");

                var computerIndex = ChooseComputerMove(game.Board);
                if (computerIndex >= 0)
                    ApplyMove(game, GameValues.O, computerIndex / 3, computerIndex % 3);
            }

            return game.Copy();
        }
    }

    public GameState Undo(Guid id)
    {
        lock (_gate)
        {
            var game = GetRequiredGame(id);
            if (game.Status != GameValues.InProgress)
                throw new InvalidOperationException("Undo is disabled after a game is completed.");
            if (game.MoveHistory.Count == 0)
                throw new InvalidOperationException("There are no moves to undo.");

            if (game.Mode == GameValues.Computer)
            {
                // Remove O's computer move and X's preceding move as a pair.
                RemoveLastMove(game);
                if (game.MoveHistory.Count > 0 &&
                    game.MoveHistory[^1].Player == GameValues.X)
                    RemoveLastMove(game);
            }
            else
            {
                RemoveLastMove(game);
            }

            RecalculateState(game);
            return game.Copy();
        }
    }

    public GameState ResetGame(Guid id)
    {
        lock (_gate)
        {
            var old = GetRequiredGame(id);
            var fresh = new GameState { Id = Guid.NewGuid(), Mode = old.Mode };
            _games.Add(fresh.Id, fresh);
            return fresh.Copy();
        }
    }

    public Scoreboard GetScoreboard()
    {
        lock (_gate)
            return new Scoreboard { XWins = _scoreboard.XWins, OWins = _scoreboard.OWins, Draws = _scoreboard.Draws };
    }

    public Scoreboard ResetScoreboard()
    {
        lock (_gate)
        {
            _scoreboard.XWins = 0;
            _scoreboard.OWins = 0;
            _scoreboard.Draws = 0;
            return GetScoreboard();
        }
    }

    public static int ChooseComputerMove(string?[] board)
    {
        // Priority 1: take a winning move.
        var move = FindTacticalMove(board, GameValues.O);
        if (move >= 0) return move;

        // Priority 2: block X's winning move.
        move = FindTacticalMove(board, GameValues.X);
        if (move >= 0) return move;

        // Priority 3: center.
        if (board[4] is null) return 4;

        // Priority 4: any corner.
        foreach (var corner in new[] { 0, 2, 6, 8 })
            if (board[corner] is null) return corner;

        // Priority 5: any available cell.
        for (var i = 0; i < board.Length; i++)
            if (board[i] is null) return i;

        return -1;
    }

    private static int FindTacticalMove(string?[] board, string player)
    {
        foreach (var line in WinningLines)
        {
            var cells = line.Select(i => board[i]).ToArray();
            if (cells.Count(v => v == player) == 2 && cells.Count(v => v is null) == 1)
                return line.First(i => board[i] is null);
        }
        return -1;
    }

    private GameState GetRequiredGame(Guid id) =>
        _games.TryGetValue(id, out var game)
            ? game
            : throw new KeyNotFoundException($"Game '{id}' was not found.");

    private static void ValidateMove(GameState game, string player, int row, int column)
    {
        if (row is < 0 or > 2 || column is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(row), "Row and column must be between 0 and 2.");
        if (game.Status != GameValues.InProgress)
            throw new InvalidOperationException("The game is already completed.");
        if (player is not (GameValues.X or GameValues.O))
            throw new ArgumentException("Player must be X or O.");
        if (game.Mode == GameValues.Computer && player != GameValues.X)
            throw new InvalidOperationException("Only player X can submit moves in Computer mode.");
        if (player != game.CurrentPlayer)
            throw new InvalidOperationException($"It is player {game.CurrentPlayer}'s turn.");
        var index = row * 3 + column;
        if (game.Board[index] is not null)
            throw new InvalidOperationException("That cell is already occupied.");
    }

    private void ApplyMove(GameState game, string player, int row, int column)
    {
        var index = row * 3 + column;
        game.Board[index] = player;
        game.MoveHistory.Add(new MoveRecord(game.MoveHistory.Count + 1, player, row, column, index));

        var winningLine = WinningLines.FirstOrDefault(line => line.All(i => game.Board[i] == player));
        if (winningLine is not null)
        {
            game.Status = GameValues.Won;
            game.Winner = player;
            game.WinningCells = winningLine.ToArray();
            CountResultOnce(game);
        }
        else if (game.Board.All(cell => cell is not null))
        {
            game.Status = GameValues.Draw;
            game.Winner = null;
            game.WinningCells = [];
            CountResultOnce(game);
        }
        else
        {
            game.CurrentPlayer = player == GameValues.X ? GameValues.O : GameValues.X;
        }
    }

    private void CountResultOnce(GameState game)
    {
        if (game.ResultCounted) return;
        if (game.Status == GameValues.Won && game.Winner == GameValues.X) _scoreboard.XWins++;
        else if (game.Status == GameValues.Won && game.Winner == GameValues.O) _scoreboard.OWins++;
        else if (game.Status == GameValues.Draw) _scoreboard.Draws++;
        game.ResultCounted = true;
    }

    private static void RemoveLastMove(GameState game)
    {
        var last = game.MoveHistory[^1];
        game.Board[last.CellIndex] = null;
        game.MoveHistory.RemoveAt(game.MoveHistory.Count - 1);
    }

    private static void RecalculateState(GameState game)
    {
        game.Status = GameValues.InProgress;
        game.Winner = null;
        game.WinningCells = [];
        game.ResultCounted = false;
        game.CurrentPlayer = game.MoveHistory.Count == 0
            ? GameValues.X
            : (game.MoveHistory[^1].Player == GameValues.X ? GameValues.O : GameValues.X);

        // Keep move numbers contiguous after undo.
        game.MoveHistory = game.MoveHistory
            .Select((m, i) => m with { MoveNumber = i + 1 })
            .ToList();
    }
}
