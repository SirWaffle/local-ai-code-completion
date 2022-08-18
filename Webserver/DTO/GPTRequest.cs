using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DTO
{
    public class GPTRequest
    {
        public string username { get; set; } = String.Empty;

        public GPTGenerationSettings generationSettings { get; set; } = new();
    }
}
