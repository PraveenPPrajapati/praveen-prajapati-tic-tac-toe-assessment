using TicTacToe.Api.Models;
using TicTacToe.Api.Services;
using Xunit;

namespace TicTacToe.Tests;

/// <summary>
/// Game-rule tests grouped by the assignment's acceptance criteria.
/// Complements <see cref="GameServiceTests"/> with broader coverage of each rule.
/// </summary>
public sealed class GameServiceAcceptanceTests
{
    private static GameService NewService() => new();

    private static GameState PlayCells(GameService s, GameState g, params int[] cells)
    {
        foreach (var cell in cells) g = s.MakeMove(g.Id, g.CurrentPlayer, cell / 3, cell % 3);
        return g;
    }

    // ---- New game ----

    [Theory]
    [InlineData(GameValues.TwoPlayer)]
    [InlineData(GameValues.Computer)]
    public void NewGameStartsEmptyWithXToMove(string mode)
    {
        var g = NewService().CreateGame(mode);
        Assert.NotEqual(Guid.Empty, g.Id);
        Assert.Equal(mode, g.Mode);
        Assert.Equal(9, g.Board.Length);
        Assert.All(g.Board, cell => Assert.Null(cell));
        Assert.Equal(GameValues.X, g.CurrentPlayer);
        Assert.Equal(GameValues.InProgress, g.Status);
        Assert.Null(g.Winner);
        Assert.Empty(g.WinningCells);
        Assert.Empty(g.MoveHistory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Online")]
    [InlineData("twoplayer")]
    public void CreateGameRejectsUnknownMode(string mode)
    {
        Assert.Throws<ArgumentException>(() => NewService().CreateGame(mode));
    }

    [Fact]
    public void EachNewGameHasItsOwnState()
    {
        var s = NewService();
        var first = s.CreateGame(GameValues.TwoPlayer);
        var second = s.CreateGame(GameValues.TwoPlayer);
        s.MakeMove(first.Id, "X", 1, 1);
        Assert.NotEqual(first.Id, second.Id);
        Assert.All(s.GetGame(second.Id).Board, cell => Assert.Null(cell));
    }

    // ---- Turns alternate ----

    [Fact]
    public void TurnsAlternateBetweenXAndOInTwoPlayerMode()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        var expected = new[] { "X", "O", "X", "O", "X" };
        var cells = new[] { 0, 4, 8, 2, 6 };
        for (var i = 0; i < cells.Length; i++)
        {
            Assert.Equal(expected[i], g.CurrentPlayer);
            g = s.MakeMove(g.Id, expected[i], cells[i] / 3, cells[i] % 3);
            Assert.Equal(expected[i], g.Board[cells[i]]);
        }
    }

    [Fact]
    public void RejectsMoveFromPlayerOutOfTurnAndLeavesBoardUnchanged()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        var ex = Assert.Throws<InvalidOperationException>(() => s.MakeMove(g.Id, "O", 0, 0));
        Assert.Contains("X", ex.Message);
        var after = s.GetGame(g.Id);
        Assert.All(after.Board, cell => Assert.Null(cell));
        Assert.Empty(after.MoveHistory);
        Assert.Equal("X", after.CurrentPlayer);
    }

    // ---- Invalid moves ----

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 0)]
    [InlineData(0, 3)]
    public void RejectsCoordinatesOutsideTheBoard(int row, int column)
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        Assert.Throws<ArgumentOutOfRangeException>(() => s.MakeMove(g.Id, "X", row, column));
        Assert.Empty(s.GetGame(g.Id).MoveHistory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Z")]
    [InlineData("x")]
    public void RejectsUnknownPlayerSymbol(string player)
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        Assert.Throws<ArgumentException>(() => s.MakeMove(g.Id, player, 0, 0));
    }

    [Fact]
    public void UnknownGameIdIsReportedAsNotFound()
    {
        var s = NewService();
        var id = Guid.NewGuid();
        Assert.Throws<KeyNotFoundException>(() => s.GetGame(id));
        Assert.Throws<KeyNotFoundException>(() => s.MakeMove(id, "X", 0, 0));
        Assert.Throws<KeyNotFoundException>(() => s.Undo(id));
        Assert.Throws<KeyNotFoundException>(() => s.ResetGame(id));
    }

    [Fact]
    public void ComputerModeRejectsMovesSubmittedForO()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.Computer);
        Assert.Throws<InvalidOperationException>(() => s.MakeMove(g.Id, "O", 0, 0));
        Assert.Empty(s.GetGame(g.Id).MoveHistory);
    }

    [Fact]
    public void ComputerModeRejectsHumanMoveOntoComputerCell()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.Computer);
        g = s.MakeMove(g.Id, "X", 0, 0); // computer answers in the center
        Assert.Throws<InvalidOperationException>(() => s.MakeMove(g.Id, "X", 1, 1));
        Assert.Equal(2, s.GetGame(g.Id).MoveHistory.Count);
    }

    // ---- Win detection + winning cells ----

    public static TheoryData<int[], int[]> AllWinningLines => new()
    {
        // X cells (winning line), O cells (two harmless replies)
        { new[] { 0, 1, 2 }, new[] { 3, 4 } },
        { new[] { 3, 4, 5 }, new[] { 0, 1 } },
        { new[] { 6, 7, 8 }, new[] { 0, 1 } },
        { new[] { 0, 3, 6 }, new[] { 1, 2 } },
        { new[] { 1, 4, 7 }, new[] { 0, 2 } },
        { new[] { 2, 5, 8 }, new[] { 0, 1 } },
        { new[] { 0, 4, 8 }, new[] { 1, 2 } },
        { new[] { 2, 4, 6 }, new[] { 0, 1 } },
    };

    [Theory]
    [MemberData(nameof(AllWinningLines))]
    public void DetectsEveryRowColumnAndDiagonalWinWithWinningCells(int[] xCells, int[] oCells)
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = PlayCells(s, g, xCells[0], oCells[0], xCells[1], oCells[1], xCells[2]);

        Assert.Equal(GameValues.Won, g.Status);
        Assert.Equal("X", g.Winner);
        Assert.Equal(xCells, g.WinningCells);
        Assert.Equal(1, s.GetScoreboard().XWins);
    }

    [Fact]
    public void DetectsWinForPlayerO()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = PlayCells(s, g, 0, 3, 1, 4, 8, 5);

        Assert.Equal(GameValues.Won, g.Status);
        Assert.Equal("O", g.Winner);
        Assert.Equal(new[] { 3, 4, 5 }, g.WinningCells);
        var score = s.GetScoreboard();
        Assert.Equal(0, score.XWins);
        Assert.Equal(1, score.OWins);
    }

    [Fact]
    public void WinOnNinthMoveIsAWinNotADraw()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        // X: 0,2,4,5,6 / O: 1,3,7,8 -> X completes diagonal 2-4-6 with a full board.
        g = PlayCells(s, g, 0, 1, 2, 3, 4, 7, 5, 8, 6);
        Assert.Equal(GameValues.Won, g.Status);
        Assert.Equal("X", g.Winner);
        Assert.Equal(0, s.GetScoreboard().Draws);
    }

    // ---- Draw detection ----

    [Fact]
    public void DrawHasNoWinnerAndNoWinningCells()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = PlayCells(s, g, 0, 1, 2, 4, 3, 5, 7, 6, 8);
        Assert.Equal(GameValues.Draw, g.Status);
        Assert.Null(g.Winner);
        Assert.Empty(g.WinningCells);
        Assert.All(g.Board, cell => Assert.NotNull(cell));
    }

    // ---- Computer mode ----

    [Fact]
    public void ComputerCanWinAndScoresForO()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.Computer);
        g = s.MakeMove(g.Id, "X", 0, 0); // O -> 4 (center)
        g = s.MakeMove(g.Id, "X", 0, 1); // O -> 2 (block)
        g = s.MakeMove(g.Id, "X", 2, 0); // O -> 3 (block)
        g = s.MakeMove(g.Id, "X", 2, 2); // O -> 5 (win 3-4-5)

        Assert.Equal(GameValues.Won, g.Status);
        Assert.Equal("O", g.Winner);
        Assert.Equal(new[] { 3, 4, 5 }, g.WinningCells);
        Assert.Equal(1, s.GetScoreboard().OWins);
    }

    [Fact]
    public void ComputerDoesNotMoveAfterHumanWins()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.Computer);
        g = s.MakeMove(g.Id, "X", 0, 0); // O -> 4 (center)
        g = s.MakeMove(g.Id, "X", 2, 2); // O -> 2 (first free corner)
        g = s.MakeMove(g.Id, "X", 2, 0); // X blocks 2-4-6; O -> 7 (blocks 6-7-8)
        g = s.MakeMove(g.Id, "X", 1, 0); // X completes 0-3-6

        Assert.Equal(GameValues.Won, g.Status);
        Assert.Equal("X", g.Winner);
        Assert.Equal(new[] { 0, 3, 6 }, g.WinningCells);
        Assert.Equal(7, g.MoveHistory.Count);
        Assert.Equal("X", g.MoveHistory[^1].Player);
        Assert.Equal(1, s.GetScoreboard().XWins);
    }

    [Fact]
    public void ComputerFallsBackToFirstFreeCellWhenCenterAndCornersAreTaken()
    {
        // X O X / _ X _ / O X O : no tactical move for either side, center and corners occupied.
        string?[] board = ["X", "O", "X", null, "X", null, "O", "X", "O"];
        Assert.Equal(3, GameService.ChooseComputerMove(board));
    }

    [Fact]
    public void ComputerReturnsMinusOneForFullBoard()
    {
        string?[] full = ["X", "O", "X", "X", "O", "O", "O", "X", "X"];
        Assert.Equal(-1, GameService.ChooseComputerMove(full));
    }

    // ---- Move history ----

    [Fact]
    public void MoveHistoryRecordsNumberPlayerRowColumnAndCell()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = s.MakeMove(g.Id, "X", 2, 1);
        g = s.MakeMove(g.Id, "O", 0, 2);

        Assert.Equal(
            new[] { new MoveRecord(1, "X", 2, 1, 7), new MoveRecord(2, "O", 0, 2, 2) },
            g.MoveHistory);
    }

    [Fact]
    public void MoveHistoryStaysContiguousAfterUndoAndReplay()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = PlayCells(s, g, 0, 4, 8);
        g = s.Undo(g.Id);
        g = PlayCells(s, g, 2);

        Assert.Equal(new[] { 1, 2, 3 }, g.MoveHistory.Select(m => m.MoveNumber));
        Assert.Equal(2, g.MoveHistory[^1].CellIndex);
        Assert.Null(g.Board[8]);
    }

    // ---- Undo by mode ----

    [Fact]
    public void UndoWithNoMovesIsRejected()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        Assert.Throws<InvalidOperationException>(() => s.Undo(g.Id));
    }

    [Fact]
    public void TwoPlayerUndoCanRewindToEmptyBoardOneMoveAtATime()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = PlayCells(s, g, 0, 4, 8);

        g = s.Undo(g.Id);
        Assert.Equal(2, g.MoveHistory.Count);
        Assert.Equal("X", g.CurrentPlayer);
        g = s.Undo(g.Id);
        Assert.Single(g.MoveHistory);
        Assert.Equal("O", g.CurrentPlayer);
        g = s.Undo(g.Id);
        Assert.Empty(g.MoveHistory);
        Assert.Equal("X", g.CurrentPlayer);
        Assert.All(g.Board, cell => Assert.Null(cell));
    }

    [Fact]
    public void ComputerUndoRemovesOnlyTheLatestPair()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.Computer);
        g = s.MakeMove(g.Id, "X", 0, 0);
        g = s.MakeMove(g.Id, "X", 2, 2);
        Assert.Equal(4, g.MoveHistory.Count);

        g = s.Undo(g.Id);
        Assert.Equal(2, g.MoveHistory.Count);
        Assert.Equal("X", g.Board[0]);
        Assert.Equal("O", g.Board[4]);
        Assert.Null(g.Board[8]);
        Assert.Equal("X", g.CurrentPlayer);
    }

    [Fact]
    public void UndoIsRejectedAfterDrawAndScoreIsKept()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = PlayCells(s, g, 0, 1, 2, 4, 3, 5, 7, 6, 8);
        Assert.Throws<InvalidOperationException>(() => s.Undo(g.Id));
        Assert.Equal(1, s.GetScoreboard().Draws);
        Assert.Equal(GameValues.Draw, s.GetGame(g.Id).Status);
    }

    // ---- Scoreboard ----

    [Fact]
    public void ScoreboardStartsAtZero()
    {
        var score = NewService().GetScoreboard();
        Assert.Equal((0, 0, 0), (score.XWins, score.OWins, score.Draws));
    }

    [Fact]
    public void ScoreboardAccumulatesAcrossGames()
    {
        var s = NewService();
        PlayCells(s, s.CreateGame(GameValues.TwoPlayer), 0, 3, 1, 4, 2);          // X wins
        PlayCells(s, s.CreateGame(GameValues.TwoPlayer), 0, 3, 1, 4, 2);          // X wins
        PlayCells(s, s.CreateGame(GameValues.TwoPlayer), 0, 3, 1, 4, 8, 5);       // O wins
        PlayCells(s, s.CreateGame(GameValues.TwoPlayer), 0, 1, 2, 4, 3, 5, 7, 6, 8); // draw

        var score = s.GetScoreboard();
        Assert.Equal(2, score.XWins);
        Assert.Equal(1, score.OWins);
        Assert.Equal(1, score.Draws);
    }

    [Fact]
    public void InProgressGamesDoNotChangeScoreboard()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        PlayCells(s, g, 0, 4, 8);
        var score = s.GetScoreboard();
        Assert.Equal((0, 0, 0), (score.XWins, score.OWins, score.Draws));
    }

    // ---- Reset game ----

    [Fact]
    public void ResetGameKeepsModeAndStartsFreshMidGame()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.Computer);
        g = s.MakeMove(g.Id, "X", 0, 0);

        var fresh = s.ResetGame(g.Id);
        Assert.NotEqual(g.Id, fresh.Id);
        Assert.Equal(GameValues.Computer, fresh.Mode);
        Assert.Equal(GameValues.InProgress, fresh.Status);
        Assert.Equal("X", fresh.CurrentPlayer);
        Assert.Empty(fresh.MoveHistory);
        Assert.All(fresh.Board, cell => Assert.Null(cell));
    }

    // ---- Reset scoreboard ----

    [Fact]
    public void ResetScoreboardDoesNotAffectCurrentGameAndCountsNewResults()
    {
        var s = NewService();
        PlayCells(s, s.CreateGame(GameValues.TwoPlayer), 0, 3, 1, 4, 2); // X wins
        var inProgress = PlayCells(s, s.CreateGame(GameValues.TwoPlayer), 0, 3, 1, 4);

        s.ResetScoreboard();
        Assert.Equal(4, s.GetGame(inProgress.Id).MoveHistory.Count);

        PlayCells(s, inProgress, 8, 5); // O wins after the reset
        var score = s.GetScoreboard();
        Assert.Equal((0, 1, 0), (score.XWins, score.OWins, score.Draws));
    }
}
