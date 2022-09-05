using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DTO
{
    public class CodeGenRequest
    {
        public string username { get; set; } = String.Empty;

        public CodeGenerationSettings generationSettings { get; set; } = new();
    }
}
