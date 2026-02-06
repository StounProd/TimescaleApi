using Microsoft.AspNetCore.Mvc;
using TimescaleApi.Application.Dtos;
using TimescaleApi.Application.Exceptions;
using TimescaleApi.Application.Services;

namespace TimescaleApi.Controllers;

[ApiController]
[Route("api")]
public class ResultsController : ControllerBase
{
    private readonly IResultsService _resultsService;

    public ResultsController(IResultsService resultsService)
    {
        _resultsService = resultsService;
    }

    [HttpGet("results")]
    [ProducesResponseType(typeof(PagedResultDto<ResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<ResultDto>>> GetResults([FromQuery] ResultsFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _resultsService.GetResultsAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("values/last10")]
    [ProducesResponseType(typeof(IReadOnlyList<ValueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ValueDto>>> GetLastValues([FromQuery] string fileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ValidationException("Имя файла не задано.");
        }

        var result = await _resultsService.GetLastValuesAsync(fileName, 10, cancellationToken);
        return Ok(result);
    }
}
