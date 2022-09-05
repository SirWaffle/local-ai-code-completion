using BlingFire;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace genLib.Tokenizers
{
    public interface ITokenizer
    {
        public void Load();
        public void Unload();

        public long[] Tokenize(string input_str, int tokenBufferSize = 2048);
        public string TokenIdsToText(List<long> ids);
    }
}
