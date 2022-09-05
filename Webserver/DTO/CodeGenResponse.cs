using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DTO
{
    public class CodeGenResponse
    {
        public CodeGenRequest? request { get; set; } = null;
        public string[]? reponseText { get; set; } = null;
    }
}
