using BlingFire;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace genLib.Tokenizers
{
    public class BlingFireTokenizer: ITokenizer
    {
        string tokenizerModelPath = String.Empty;
        string tokenizerModelDecodePath = String.Empty;

        ulong tokenizerHandle = 0;
        ulong tokenDecoderHandle = 0;

        public BlingFireTokenizer(string encodeModelPath, string decodeModelPath)
        {
            tokenizerModelPath = encodeModelPath;
            tokenizerModelDecodePath = decodeModelPath;
        }

        public void Load()
        {
            tokenizerHandle = BlingFireUtils2.LoadModel(tokenizerModelPath);
            BlingFireUtils2.SetNoDummyPrefix(tokenizerHandle, false);

            tokenDecoderHandle = BlingFireUtils2.LoadModel(tokenizerModelDecodePath);
            BlingFireUtils2.SetNoDummyPrefix(tokenDecoderHandle, false);
        }

        public void Unload()
        {
            BlingFireUtils2.FreeModel(tokenizerHandle);
            BlingFireUtils2.FreeModel(tokenDecoderHandle);
        }

        public long[] Tokenize(string input_str, int tokenBufferSize = 2048)
        {
            byte[] inBytes = System.Text.Encoding.UTF8.GetBytes(input_str);
            int[] ids = new int[tokenBufferSize];
            int outputCount = BlingFireUtils2.TextToIds(tokenizerHandle, inBytes, inBytes.Length, ids, ids.Length, 0);
            return ids.Take(outputCount).Select(item => (long)item).ToArray();
        }

        public string TokenIdsToText(List<long> ids)
        {
            int[] tokArray = ids.Select(item => (int)item).ToArray();
            string text = BlingFireUtils2.IdsToText(tokenDecoderHandle, tokArray);
            return text;
        }
    }
}
