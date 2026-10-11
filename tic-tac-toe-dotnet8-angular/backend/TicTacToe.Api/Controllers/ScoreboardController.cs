using Microsoft.AspNetCore.Mvc;
using TicTacToe.Api.Models;
using TicTacToe.Api.Services;

namespace TicTacToe.Api.Controllers;

[ApiController]
[Route("api/scoreboard")]
[Produces("application/json")]
public sealed class ScoreboardController(GameService gameService) : ControllerBase
{
    /// <summary>Gets the session scoreboard (X wins, O wins, draws).</summary>
    [HttpGet]
    [ProducesResponseType<Scoreboard>(StatusCodes.Status200OK)]
    public ActionResult<Scoreboard> Get() => Ok(gameService.GetScoreboard());

    /// <summary>Resets all scoreboard counters to zero.</summary>
    [HttpPost("reset")]
    [ProducesResponseType<Scoreboard>(StatusCodes.Status200OK)]
    public ActionResult<Scoreboard> Reset() => Ok(gameService.ResetScoreboard());
}
