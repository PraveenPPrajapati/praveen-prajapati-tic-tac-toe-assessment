using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TicTacToe.Tests;

/// <summary>
/// End-to-end HTTP tests: the real ASP.NET Core pipeline (routing, model binding,
/// JSON serialization, status codes, CORS) hosted in memory.
/// xUnit creates a new class instance per test, so each test gets its own
/// in-memory GameService and scoreboard.
/// </summary>
public sealed class GamesApiTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory =
        new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("https_port", "7080"));
    private readonly HttpClient _client;

    // The Angular app calls the API over HTTPS, so the test client does too.
    public GamesApiTests() =>
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---- DTOs mirroring the JSON contract used by the Angular app ----

    private sealed record MoveDto(int MoveNumber, string Player, int Row, int Column, int CellIndex);

    private sealed record GameDto(
        Guid Id, string?[] Board, string CurrentPlayer, string Mode, string Status,
        string? Winner, int[] WinningCells, List<MoveDto> MoveHistory);

    private sealed record ScoreDto(int XWins, int OWins, int Draws);

    private sealed record ErrorDto(string Message);

    // ---- Helpers ----

    private async Task<GameDto> CreateGame(string mode = "TwoPlayer")
    {
        var response = await _client.PostAsJsonAsync("/api/games", new { mode });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GameDto>())!;
    }

    private Task<HttpResponseMessage> PostMove(Guid id, string player, int row, int column) =>
        _client.PostAsJsonAsync($"/api/games/{id}/moves", new { player, row, column });

    private async Task<GameDto> Move(Guid id, string player, int row, int column)
    {
        var response = await PostMove(id, player, row, column);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GameDto>())!;
    }

    private async Task<GameDto> PlayXWinTopRow(Guid id)
    {
        await Move(id, "X", 0, 0);
        await Move(id, "O", 1, 0);
        await Move(id, "X", 0, 1);
        await Move(id, "O", 1, 1);
        return await Move(id, "X", 0, 2);
    }

    private async Task<ScoreDto> GetScore() =>
        (await _client.GetFromJsonAsync<ScoreDto>("/api/scoreboard"))!;

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.False(string.IsNullOrWhiteSpace(error?.Message));
    }

    // ---- API runs / frontend can talk to it ----

    [Fact]
    public async Task RootEndpointReportsRunning()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Running", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PlainHttpIsRedirectedToHttps()
    {
        using var httpClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false
        });

        var response = await httpClient.GetAsync("/api/scoreboard");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal("https://localhost:7080/api/scoreboard", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("https://localhost:4200")]
    [InlineData("http://localhost:4200")]
    public async Task CorsAllowsTheAngularDevServer(string origin)
    {
        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/games");
        preflight.Headers.Add("Origin", origin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await _client.SendAsync(preflight);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task CorsDoesNotAllowUnknownOrigins()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/scoreboard");
        request.Headers.Add("Origin", "http://evil.example");
        var response = await _client.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ResponsesUseCamelCaseJsonExpectedByAngular()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new { mode = "TwoPlayer" });
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        foreach (var name in new[] { "id", "board", "currentPlayer", "mode", "status", "winner", "winningCells", "moveHistory" })
            Assert.True(root.TryGetProperty(name, out _), $"Missing JSON property '{name}'.");
    }

    [Fact]
    public async Task SwaggerDocumentListsEveryEndpoint()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = json.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.Equal(
            new HashSet<string>
            {
                "/api/games", "/api/games/{id}", "/api/games/{id}/moves", "/api/games/{id}/undo",
                "/api/games/{id}/reset", "/api/scoreboard", "/api/scoreboard/reset"
            },
            paths);
    }

    [Fact]
    public async Task SwaggerUiIsServed()
    {
        var response = await _client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Create / get game ----

    [Theory]
    [InlineData("TwoPlayer")]
    [InlineData("Computer")]
    public async Task CreateGameReturnsFreshBoard(string mode)
    {
        var game = await CreateGame(mode);
        Assert.Equal(mode, game.Mode);
        Assert.Equal("InProgress", game.Status);
        Assert.Equal("X", game.CurrentPlayer);
        Assert.Equal(9, game.Board.Length);
        Assert.All(game.Board, cell => Assert.Null(cell));
        Assert.Empty(game.MoveHistory);
    }

    [Fact]
    public async Task CreateGameWithInvalidModeReturns400()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new { mode = "Chess" });
        await AssertError(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetGameReturnsCurrentState()
    {
        var game = await CreateGame();
        await Move(game.Id, "X", 1, 1);
        var fetched = await _client.GetFromJsonAsync<GameDto>($"/api/games/{game.Id}");
        Assert.Equal("X", fetched!.Board[4]);
        Assert.Equal("O", fetched.CurrentPlayer);
    }

    [Fact]
    public async Task UnknownGameReturns404OnEveryGameEndpoint()
    {
        var id = Guid.NewGuid();
        await AssertError(await _client.GetAsync($"/api/games/{id}"), HttpStatusCode.NotFound);
        await AssertError(await PostMove(id, "X", 0, 0), HttpStatusCode.NotFound);
        await AssertError(await _client.PostAsync($"/api/games/{id}/undo", null), HttpStatusCode.NotFound);
        await AssertError(await _client.PostAsync($"/api/games/{id}/reset", null), HttpStatusCode.NotFound);
    }

    // ---- Moves, turns and invalid moves ----

    [Fact]
    public async Task ValidMovesAlternateTurns()
    {
        var game = await CreateGame();
        game = await Move(game.Id, "X", 0, 0);
        Assert.Equal("O", game.CurrentPlayer);
        game = await Move(game.Id, "O", 1, 1);
        Assert.Equal("X", game.CurrentPlayer);
        Assert.Equal(new[] { "X", "O" }, game.MoveHistory.Select(m => m.Player));
    }

    [Fact]
    public async Task OccupiedCellReturns409()
    {
        var game = await CreateGame();
        await Move(game.Id, "X", 0, 0);
        await AssertError(await PostMove(game.Id, "O", 0, 0), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task WrongTurnReturns409()
    {
        var game = await CreateGame();
        await AssertError(await PostMove(game.Id, "O", 0, 0), HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(0, -1)]
    public async Task OutOfRangeMoveReturns400(int row, int column)
    {
        var game = await CreateGame();
        await AssertError(await PostMove(game.Id, "X", row, column), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidPlayerReturns400()
    {
        var game = await CreateGame();
        await AssertError(await PostMove(game.Id, "Q", 0, 0), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MoveAfterGameOverReturns409()
    {
        var game = await CreateGame();
        await PlayXWinTopRow(game.Id);
        await AssertError(await PostMove(game.Id, "O", 2, 2), HttpStatusCode.Conflict);
    }

    // ---- Win / draw ----

    [Fact]
    public async Task WinReturnsWinnerAndWinningCellsAndUpdatesScoreboard()
    {
        var game = await CreateGame();
        game = await PlayXWinTopRow(game.Id);

        Assert.Equal("Won", game.Status);
        Assert.Equal("X", game.Winner);
        Assert.Equal(new[] { 0, 1, 2 }, game.WinningCells);
        Assert.Equal(new ScoreDto(1, 0, 0), await GetScore());
    }

    [Fact]
    public async Task DrawIsReportedAndCounted()
    {
        var game = await CreateGame();
        foreach (var (p, cell) in new[] { ("X", 0), ("O", 1), ("X", 2), ("O", 4), ("X", 3), ("O", 5), ("X", 7), ("O", 6), ("X", 8) })
            game = await Move(game.Id, p, cell / 3, cell % 3);

        Assert.Equal("Draw", game.Status);
        Assert.Null(game.Winner);
        Assert.Empty(game.WinningCells);
        Assert.Equal(new ScoreDto(0, 0, 1), await GetScore());
    }

    // ---- Computer mode ----

    [Fact]
    public async Task ComputerRespondsInTheSameRequest()
    {
        var game = await CreateGame("Computer");
        game = await Move(game.Id, "X", 0, 0);

        Assert.Equal(2, game.MoveHistory.Count);
        Assert.Equal("O", game.MoveHistory[1].Player);
        Assert.Equal("O", game.Board[4]);
        Assert.Equal("X", game.CurrentPlayer);
    }

    [Fact]
    public async Task ComputerModeRejectsMovesForO()
    {
        var game = await CreateGame("Computer");
        await AssertError(await PostMove(game.Id, "O", 0, 0), HttpStatusCode.Conflict);
    }

    // ---- Undo ----

    [Fact]
    public async Task UndoInTwoPlayerModeRemovesOneMove()
    {
        var game = await CreateGame();
        await Move(game.Id, "X", 0, 0);
        await Move(game.Id, "O", 1, 1);

        var response = await _client.PostAsync($"/api/games/{game.Id}/undo", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var undone = (await response.Content.ReadFromJsonAsync<GameDto>())!;
        Assert.Single(undone.MoveHistory);
        Assert.Null(undone.Board[4]);
        Assert.Equal("O", undone.CurrentPlayer);
    }

    [Fact]
    public async Task UndoInComputerModeRemovesHumanAndComputerMoves()
    {
        var game = await CreateGame("Computer");
        await Move(game.Id, "X", 0, 0);

        var undone = (await (await _client.PostAsync($"/api/games/{game.Id}/undo", null))
            .Content.ReadFromJsonAsync<GameDto>())!;
        Assert.Empty(undone.MoveHistory);
        Assert.All(undone.Board, cell => Assert.Null(cell));
        Assert.Equal("X", undone.CurrentPlayer);
    }

    [Fact]
    public async Task UndoWithNoMovesReturns409()
    {
        var game = await CreateGame();
        await AssertError(await _client.PostAsync($"/api/games/{game.Id}/undo", null), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UndoAfterWinReturns409AndKeepsScore()
    {
        var game = await CreateGame();
        await PlayXWinTopRow(game.Id);
        await AssertError(await _client.PostAsync($"/api/games/{game.Id}/undo", null), HttpStatusCode.Conflict);
        Assert.Equal(new ScoreDto(1, 0, 0), await GetScore());
    }

    // ---- Reset game / scoreboard ----

    [Fact]
    public async Task ResetGameStartsNewGameWithSameModeAndKeepsScore()
    {
        var game = await CreateGame("Computer");
        await Move(game.Id, "X", 0, 0);
        await Move(game.Id, "X", 2, 2);

        var won = await CreateGame();
        await PlayXWinTopRow(won.Id);

        var response = await _client.PostAsync($"/api/games/{game.Id}/reset", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fresh = (await response.Content.ReadFromJsonAsync<GameDto>())!;

        Assert.NotEqual(game.Id, fresh.Id);
        Assert.Equal("Computer", fresh.Mode);
        Assert.Empty(fresh.MoveHistory);
        Assert.All(fresh.Board, cell => Assert.Null(cell));
        Assert.Equal(new ScoreDto(1, 0, 0), await GetScore());
    }

    [Fact]
    public async Task ScoreboardStartsAtZeroAndResetClearsIt()
    {
        Assert.Equal(new ScoreDto(0, 0, 0), await GetScore());

        var game = await CreateGame();
        await PlayXWinTopRow(game.Id);
        Assert.Equal(new ScoreDto(1, 0, 0), await GetScore());

        var response = await _client.PostAsync("/api/scoreboard/reset", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new ScoreDto(0, 0, 0), await response.Content.ReadFromJsonAsync<ScoreDto>());
        Assert.Equal(new ScoreDto(0, 0, 0), await GetScore());
    }
}
