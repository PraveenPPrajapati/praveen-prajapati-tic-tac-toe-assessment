using Microsoft.AspNetCore.Mvc;
using TicTacToe.Api.Models;
using TicTacToe.Api.Services;

namespace TicTacToe.Api.Controllers;

[ApiController]
[Route("api/games")]
[Produces("application/json")]
public sealed class GamesController(GameService gameService) : ControllerBase
{
    /// <summary>Creates a new game.</summary>
    /// <remarks>Mode is <c>TwoPlayer</c> or <c>Computer</c>. In Computer mode the human is X and the computer is O.</remarks>
    [HttpPost]
    [ProducesResponseType<GameState>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public ActionResult<GameState> Create([FromBody] CreateGameRequest request)
    {
        try { return Ok(gameService.CreateGame(request.Mode)); }
        catch (ArgumentException ex) { return BadRequest(new ErrorResponse(ex.Message)); }
    }

    /// <summary>Gets the current state of a game.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GameState>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public ActionResult<GameState> Get(Guid id)
    {
        try { return Ok(gameService.GetGame(id)); }
        catch (KeyNotFoundException ex) { return NotFound(new ErrorResponse(ex.Message)); }
    }

    /// <summary>Submits a move for the current player.</summary>
    /// <remarks>
    /// Row and column are zero-based (0-2). In Computer mode only X may move; the computer's
    /// reply is applied in the same request.
    /// Returns 400 for out-of-range coordinates or an unknown player, and 409 for an occupied cell,
    /// the wrong player's turn, or a completed game.
    /// </remarks>
    [HttpPost("{id:guid}/moves")]
    [ProducesResponseType<GameState>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public ActionResult<GameState> Move(Guid id, [FromBody] MoveRequest request)
    {
        try { return Ok(gameService.MakeMove(id, request.Player, request.Row, request.Column)); }
        catch (KeyNotFoundException ex) { return NotFound(new ErrorResponse(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(new ErrorResponse(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(new ErrorResponse(ex.Message)); }
    }

    /// <summary>Undoes the last move (Two Player) or the last human/computer pair (Computer).</summary>
    /// <remarks>Returns 409 when there are no moves or the game is already completed.</remarks>
    [HttpPost("{id:guid}/undo")]
    [ProducesResponseType<GameState>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public ActionResult<GameState> Undo(Guid id)
    {
        try { return Ok(gameService.Undo(id)); }
        catch (KeyNotFoundException ex) { return NotFound(new ErrorResponse(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(new ErrorResponse(ex.Message)); }
    }

    /// <summary>Starts a fresh game (new id) with the same mode. The scoreboard is unchanged.</summary>
    [HttpPost("{id:guid}/reset")]
    [ProducesResponseType<GameState>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public ActionResult<GameState> Reset(Guid id)
    {
        try { return Ok(gameService.ResetGame(id)); }
        catch (KeyNotFoundException ex) { return NotFound(new ErrorResponse(ex.Message)); }
    }
}
