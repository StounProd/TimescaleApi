using Microsoft.AspNetCore.Mvc;
using TimescaleApi.Application.Dtos;
using TimescaleApi.Application.Exceptions;
using TimescaleApi.Application.Services;

namespace TimescaleApi.Controllers;

[ApiController]
[Route("api/import")]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;

    public ImportController(IImportService importService)
    {
        _importService = importService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResultDto>> Import([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            throw new ValidationException("Файл не задан или пуст.");
        }

        await using var stream = file.OpenReadStream();
        var result = await _importService.ImportCsvAsync(stream, file.FileName, cancellationToken);
        return Ok(result);
    }
}
