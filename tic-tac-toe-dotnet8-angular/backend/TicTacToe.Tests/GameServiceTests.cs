using TicTacToe.Api.Models;
using TicTacToe.Api.Services;
using Xunit;

namespace TicTacToe.Tests;

public sealed class GameServiceTests
{
    private static GameService NewService() => new();
    private static GameState Play(GameService s, GameState g, params (string p, int r, int c)[] moves)
    {
        foreach (var m in moves) g = s.MakeMove(g.Id, m.p, m.r, m.c);
        return g;
    }

    [Fact]
    public void ValidMovePlacesMarkAndSwitchesTurn()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = s.MakeMove(g.Id, "X", 0, 0);
        Assert.Equal("X", g.Board[0]);
        Assert.Equal("O", g.CurrentPlayer);
        Assert.Single(g.MoveHistory);
    }

    [Fact]
    public void RejectsOccupiedCellAndPreservesTurn()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        s.MakeMove(g.Id, "X", 0, 0);
        Assert.Throws<InvalidOperationException>(() => s.MakeMove(g.Id, "O", 0, 0));
        Assert.Equal("O", s.GetGame(g.Id).CurrentPlayer);
    }

    [Fact]
    public void RejectsOutOfRangeMove()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        Assert.Throws<ArgumentOutOfRangeException>(() => s.MakeMove(g.Id, "X", 3, 0));
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(3, 4, 5)]
    [InlineData(6, 7, 8)]
    public void DetectsRowWins(int first, int second, int third)
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        var xCells = new[] { first, second, third };
        var oCells = Enumerable.Range(0, 9).Where(i => !xCells.Contains(i)).Take(2).ToArray();
        g = s.MakeMove(g.Id, "X", xCells[0] / 3, xCells[0] % 3);
        g = s.MakeMove(g.Id, "O", oCells[0] / 3, oCells[0] % 3);
        g = s.MakeMove(g.Id, "X", xCells[1] / 3, xCells[1] % 3);
        g = s.MakeMove(g.Id, "O", oCells[1] / 3, oCells[1] % 3);
        g = s.MakeMove(g.Id, "X", xCells[2] / 3, xCells[2] % 3);
        Assert.Equal(GameValues.Won, g.Status);
        Assert.Equal("X", g.Winner);
        Assert.Equal(3, g.WinningCells.Length);
        Assert.Equal(1, s.GetScoreboard().XWins);
    }

    [Fact]
    public void DetectsColumnWin()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = Play(s, g, ("X",0,0), ("O",0,1), ("X",1,0), ("O",1,1), ("X",2,0));
        Assert.Equal(GameValues.Won, g.Status);
        Assert.Equal(new[] { 0, 3, 6 }, g.WinningCells);
    }

    [Fact]
    public void DetectsDiagonalWin()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = Play(s, g, ("X",0,0), ("O",0,1), ("X",1,1), ("O",0,2), ("X",2,2));
        Assert.Equal(GameValues.Won, g.Status);
        Assert.Equal("X", g.Winner);
    }

    [Fact]
    public void DetectsDrawAndCountsItOnce()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = Play(s, g, ("X",0,0), ("O",0,1), ("X",0,2), ("O",1,1),
            ("X",1,0), ("O",1,2), ("X",2,1), ("O",2,0), ("X",2,2));
        Assert.Equal(GameValues.Draw, g.Status);
        Assert.Equal(1, s.GetScoreboard().Draws);
        Assert.Throws<InvalidOperationException>(() => s.MakeMove(g.Id, "O", 2, 1));
        Assert.Equal(1, s.GetScoreboard().Draws);
    }

    [Fact]
    public void ResetCreatesFreshBoardAndKeepsScore()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = Play(s, g, ("X",0,0), ("O",1,0), ("X",0,1), ("O",1,1), ("X",0,2));
        var before = s.GetScoreboard();
        var fresh = s.ResetGame(g.Id);
        Assert.NotEqual(g.Id, fresh.Id);
        Assert.All(fresh.Board, cell => Assert.Null(cell));
        Assert.Empty(fresh.MoveHistory);
        Assert.Equal("X", fresh.CurrentPlayer);
        Assert.Equal(before.XWins, s.GetScoreboard().XWins);
    }

    [Fact]
    public void UndoTwoPlayerRemovesOnlyLastMove()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = Play(s, g, ("X",0,0), ("O",1,1));
        g = s.Undo(g.Id);
        Assert.Null(g.Board[4]);
        Assert.Single(g.MoveHistory);
        Assert.Equal("O", g.CurrentPlayer);
    }

    [Fact]
    public void UndoComputerModeRemovesHumanAndComputerPair()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.Computer);
        g = s.MakeMove(g.Id, "X", 0, 0);
        Assert.Equal(2, g.MoveHistory.Count);
        g = s.Undo(g.Id);
        Assert.Empty(g.MoveHistory);
        Assert.All(g.Board, cell => Assert.Null(cell));
        Assert.Equal("X", g.CurrentPlayer);
    }

    [Fact]
    public void ComputerTakesWinningMoveBeforeOtherPriorities()
    {
        string?[] board = ["O", "O", null, "X", "X", null, null, null, null];
        Assert.Equal(2, GameService.ChooseComputerMove(board));
    }

    [Fact]
    public void ComputerBlocksXBeforeTakingCenter()
    {
        string?[] board = ["X", "X", null, null, null, null, null, null, null];
        Assert.Equal(2, GameService.ChooseComputerMove(board));
    }

    [Fact]
    public void ComputerChoosesCenterThenCorner()
    {
        Assert.Equal(4, GameService.ChooseComputerMove(new string?[9]));
        string?[] board = [null, "X", null, null, "O", null, null, null, null];
        Assert.Contains(GameService.ChooseComputerMove(board), new[] { 0, 2, 6, 8 });
    }

    [Fact]
    public void RejectsMoveAfterCompletion()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = Play(s, g, ("X",0,0), ("O",1,0), ("X",0,1), ("O",1,1), ("X",0,2));
        Assert.Throws<InvalidOperationException>(() => s.MakeMove(g.Id, "O", 2, 2));
    }

    [Fact]
    public void UndoIsDisabledAfterCompletion()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        g = Play(s, g, ("X",0,0), ("O",1,0), ("X",0,1), ("O",1,1), ("X",0,2));
        Assert.Throws<InvalidOperationException>(() => s.Undo(g.Id));
        Assert.Equal(1, s.GetScoreboard().XWins);
    }

    [Fact]
    public void ComputerRespondsAutomaticallyAsO()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.Computer);
        g = s.MakeMove(g.Id, "X", 0, 0);
        Assert.Equal(2, g.MoveHistory.Count);
        Assert.Equal("X", g.CurrentPlayer);
        Assert.Equal("O", g.Board[4]); // center has priority after X takes a corner
    }

    [Fact]
    public void ScoreboardCanBeReset()
    {
        var s = NewService();
        var g = s.CreateGame(GameValues.TwoPlayer);
        Play(s, g, ("X",0,0), ("O",1,0), ("X",0,1), ("O",1,1), ("X",0,2));
        var score = s.ResetScoreboard();
        Assert.Equal(0, score.XWins);
        Assert.Equal(0, score.OWins);
        Assert.Equal(0, score.Draws);
    }
}
