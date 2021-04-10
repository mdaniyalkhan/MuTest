using System;
using System.Threading.Tasks;
using Firebase.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MuTest.Firebase.Api.Models;
using MuTest.Firebase.Api.Services;

namespace MuTest.Firebase.Api.Controllers
{
    [ApiController]
    [Route("[controller]/v1")]
    [Produces("application/json")]
    public class StorageController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly IStorageService _storageService;

        public StorageController(ILogger<StorageController> logger, IStorageService service)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storageService = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpPost]
        [Route("{id}/Store")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Store(string id, IFormFile file)
        {
            try
            {
                if (CheckIfJsonFile(file))
                {
                    await _storageService.AddAsync(id, file);
                }
                else
                {
                    return BadRequest(new
                    {
                        message = "Invalid file extension"
                    });
                }
            }
            catch (Exception exp)
            {
                _logger.LogError(exp.StackTrace);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Ok();
        }

        [HttpGet]
        [Route("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Get(string id)
        {
            MutationFileResult result;
            try
            {
                var downloadUrl = await _storageService.GetAsync(id);

                result = new MutationFileResult
                {
                    DownloadUrl = downloadUrl
                };
            }
            catch (FirebaseStorageException exp)
            {
                _logger.LogError(exp.StackTrace);
                return NotFound();
            }
            catch (Exception exp)
            {
                _logger.LogError(exp.StackTrace);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return StatusCode(StatusCodes.Status200OK, result);
        }

        private bool CheckIfJsonFile(IFormFile file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            var extension = "." + file.FileName.Split('.')[file.FileName.Split('.').Length - 1];
            return extension == ".json" || 
                   extension == ".json";
        }
    }
}
