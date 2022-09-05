using airtist_webserver.Generators;
using DTO;
using Microsoft.AspNetCore.Mvc;

namespace webserver.Controllers
{
    [ApiController]
    [Route("codegen")]
    public class CodeGenController : ControllerBase
    {
        private readonly ILogger<CodeGenController> _logger;
        private readonly IGeneratorSingleton _gen;

        public CodeGenController(ILogger<CodeGenController> logger, IGeneratorSingleton generator)
        {
            _logger = logger;
            _gen = generator;
        }


        [HttpPost]
        [Route("generate")]
        public async Task<CodeGenResponse> Generate([FromBody]  CodeGenRequest req)
        {
            CodeGenResponse resp = new();
            resp.request = req;
            var gen = await _gen.DoInference(req);
            if (gen != null)
                resp.reponseText = gen.ToArray();
            else
                resp.reponseText = null;
            return resp;
        }
    }
}