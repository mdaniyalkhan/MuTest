using System;
using System.Threading.Tasks;
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
    public class FirestoreController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly IFirestoreService _fireStoreService;

        public FirestoreController(ILogger<DatabaseController> logger, IFirestoreService service)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fireStoreService = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpPost]
        [Route("Store")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Store(MutationResult model)
        {
            string recordId;
            try
            {
                recordId = await _fireStoreService.AddAsync(model);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.StackTrace);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return StatusCode(StatusCodes.Status201Created, new MutationResponse { Id = recordId });
        }

        [HttpGet]
        [Route("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Get(string id)
        {
            MutationResult result;
            try
            {
                result = await _fireStoreService.GetAsync(id);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.StackTrace);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            if (result == null)
            {
                return NotFound();
            }

            return StatusCode(StatusCodes.Status200OK, result);
        }
    }
}
