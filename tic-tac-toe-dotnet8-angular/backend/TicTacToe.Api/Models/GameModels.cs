namespace TicTacToe.Api.Models;

public static class GameValues
{
    public const string X = "X";
    public const string O = "O";
    public const string TwoPlayer = "TwoPlayer";
    public const string Computer = "Computer";
    public const string InProgress = "InProgress";
    public const string Won = "Won";
    public const string Draw = "Draw";
}

public sealed class CreateGameRequest
{
    /// <summary>Game mode: <c>TwoPlayer</c> or <c>Computer</c>.</summary>
    /// <example>TwoPlayer</example>
    public string Mode { get; set; } = GameValues.TwoPlayer;
}

public sealed class MoveRequest
{
    /// <summary>Player making the move: <c>X</c> or <c>O</c>. Must match the game's current player.</summary>
    /// <example>X</example>
    public string Player { get; set; } = "";

    /// <summary>Zero-based row (0-2).</summary>
    /// <example>0</example>
    public int Row { get; set; }

    /// <summary>Zero-based column (0-2).</summary>
    /// <example>0</example>
    public int Column { get; set; }
}

public sealed record MoveRecord(int MoveNumber, string Player, int Row, int Column, int CellIndex);

public sealed class GameState
{
    public Guid Id { get; set; }
    public string?[] Board { get; set; } = new string?[9];
    public string CurrentPlayer { get; set; } = GameValues.X;
    public string Mode { get; set; } = GameValues.TwoPlayer;
    public string Status { get; set; } = GameValues.InProgress;
    public string? Winner { get; set; }
    public int[] WinningCells { get; set; } = [];
    public List<MoveRecord> MoveHistory { get; set; } = [];
    public bool ResultCounted { get; set; }

    public GameState Copy() => new()
    {
        Id = Id,
        Board = Board.ToArray(),
        CurrentPlayer = CurrentPlayer,
        Mode = Mode,
        Status = Status,
        Winner = Winner,
        WinningCells = WinningCells.ToArray(),
        MoveHistory = MoveHistory.ToList(),
        ResultCounted = ResultCounted
    };
}

public sealed class Scoreboard
{
    public int XWins { get; set; }
    public int OWins { get; set; }
    public int Draws { get; set; }
}

/// <summary>Error body returned with 4xx responses.</summary>
public sealed record ErrorResponse(string Message);
