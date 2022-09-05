using BlingFire;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace genLib.Tokenizers
{
    public class HFTokenizer: ITokenizer
    {
        string path;
        IntPtr tokenizer;

        public HFTokenizer(string tokenizerPath)
        {
            path = tokenizerPath;
        }

        public void Load()
        {
            tokenizer = HFTokenizerInterop.from_file(path);
        }

        public void Unload()
        {
            throw new NotImplementedException();
        }

        public long[] Tokenize(string input_str, int tokenBufferSize = 2048)
        {
            uint[] ids = new uint[tokenBufferSize];
            UInt32 ntoks = HFTokenizerInterop.encode(tokenizer, input_str, ids, tokenBufferSize, false);
            return ids.Take((int)ntoks).Select(item => (long)item).ToArray();
        }

        public string TokenIdsToText(List<long> ids)
        {
            uint[] tokArray = ids.Select(item => (uint)item).ToArray();
            return HFTokenizerInterop.decode(tokenizer, tokArray, (uint)tokArray.Length, false);
        }
    }
}
