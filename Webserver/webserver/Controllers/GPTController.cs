using DTO;
using Microsoft.AspNetCore.Mvc;

namespace webserver.Controllers
{
    [ApiController]
    [Route("gpt")]
    public class GPTController : ControllerBase
    {
        private readonly ILogger<GPTController> _logger;
        private readonly IGeneratorSingleton _gen;

        public GPTController(ILogger<GPTController> logger, IGeneratorSingleton generator)
        {
            _logger = logger;
            _gen = generator;
        }

        [HttpPost]
        [Route("generate")]
        public async Task<GPTResponse> Generate([FromBody]  GPTRequest req)
        {
            GPTResponse resp = new();
            resp.request = req;
            var gen = await _gen.DoInference(req.generationSettings);
            resp.reponseText = gen.ToArray();
            return resp;
        }
    }
}