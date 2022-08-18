using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using BlingFire;
using DTO;

namespace genLib
{
    public class GPTOnnx
    {
        string tokenizerModelPath = String.Empty;
        string tokenizerModelDecodePath = String.Empty;

        string modelPath = String.Empty;
        int gpuDeviceId = 0;

        ulong tokenizerHandle = 0;
        ulong tokenDecoderHandle = 0;

        InferenceSession? inferenceSession;

        public void Load(int gpuDevice, string onnxModelPath, string tokenizerPath, string detokenizerPath)
        {
            tokenizerModelPath = tokenizerPath;
            tokenizerModelDecodePath= detokenizerPath;
            gpuDeviceId = gpuDevice;
            modelPath = onnxModelPath;

            //load model
            SessionOptions so = new SessionOptions();
            so.AppendExecutionProvider_CUDA(gpuDeviceId);
            so.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            inferenceSession = new InferenceSession(modelPath, so);

            tokenizerHandle = BlingFireUtils2.LoadModel(tokenizerModelPath);
            BlingFireUtils2.SetNoDummyPrefix(tokenizerHandle, false);

            tokenDecoderHandle = BlingFireUtils2.LoadModel(tokenizerModelDecodePath);
            BlingFireUtils2.SetNoDummyPrefix(tokenDecoderHandle, false);
        }

        public void Unload()
        {
            inferenceSession?.Dispose();
            BlingFireUtils2.FreeModel(tokenizerHandle);
            BlingFireUtils2.FreeModel(tokenDecoderHandle);
        }

        public int[] Tokenize(string input_str, int tokenBufferSize = 2048)
        {
            byte[] inBytes = System.Text.Encoding.UTF8.GetBytes(input_str);
            int[] ids = new int[tokenBufferSize];
            int outputCount = BlingFireUtils2.TextToIds(tokenizerHandle, inBytes, inBytes.Length, ids, ids.Length, 0);
            return ids.Take(outputCount).ToArray();
        }

        public string TokenIdsToText(ref int[] ids)
        {
            string text = BlingFireUtils2.IdsToText(tokenDecoderHandle, ids);            
            return text;
        }

        private List<ValueTuple<int, float>> ToDescendingIntFloatTupleList(float[] vals)
        {
            return vals
                .Select((val, index) => (Index: index, Value: val))
                .OrderByDescending(result => result.Value)
                .ToList();
        }

        private float[] Softmax(ref float[] values)
        {
            var maxVal = values.Max();
            var exp = values.Select(v => Math.Exp(v - maxVal));
            var sumExp = exp.Sum();

            return exp.Select(v => (float)(v / sumExp)).ToArray();
        }

        private void InPlaceSoftmax(ref float[] values)
        {
            float maxVal = values.Max();
            var exp = values.Select(v => Math.Exp(v - maxVal));
            var sumExp = exp.Sum();

            for (int i = 0; i < values.Length; i++)
                values[i] = (float)(Math.Exp(values[i] - maxVal) / sumExp);
        }

        private List<ValueTuple<int, float>> TopKResults(List<ValueTuple<int, float>> orderedList, int top_k = 1)
        {
            return orderedList
                .Take(top_k)
                .ToList();
        }

        private List<ValueTuple<int, float>> TopPResults(List<ValueTuple<int, float>> orderedList, float top_p = 0.9f)
        {
            float runningTotal = 0.0f;
            List<ValueTuple<int, float>> found = new();

            for (int i = 0; i < orderedList.Count; i++)
            {
                if (runningTotal >= top_p)
                    break;

                var item = orderedList[i];
                found.Add(item);

                runningTotal += item.Item2;
            }

            return found;
        }

        private ValueTuple<int, float> SelectByProbability(List<ValueTuple<int, float>> valList)
        {
            //sum total probability space...
            float totalSpace = 0.0f;
            for (int probInd = 0; probInd < valList.Count; probInd++)
            {
                totalSpace += valList[probInd].Item2;
            }

            //select a random number in that space
            float randNum = Random.Shared.NextSingle();

            //noprmalize to the total space we have
            float rand = randNum * totalSpace;

            //find the entry that contains this prob in its range
            float endProb = 0.0f;
            int selectedInd = -1;
            int lastIndWithValue = 0;
            for (int probInd = 0; probInd < valList.Count; probInd++)
            {
                if (valList[probInd].Item2 <= 0.0f)
                    continue;

                endProb += valList[probInd].Item2;
                lastIndWithValue = probInd;
                if (rand <= endProb)
                {
                    selectedInd = probInd;
                    break;
                }
            }

            if (selectedInd == -1)
                selectedInd = lastIndWithValue;

            return valList[selectedInd];
        }


        //TODO: add batches
        //TODO: early out on stopping token
        //TODO: convert stopping criteria into an array of arrays of tokens
        //      then if we generate any of those sequences of tokens, stop generating and return
        //      stopping sequences for code will most likely be various newlines, escaped and non-escaped
        public List<string> DoInference(GPTGenerationSettings gen)
        {
            //tokenize
            int[] promptTokenIds = Tokenize(gen.prompt);

            //convert to long, make attention mask
            List<long> toks = promptTokenIds.Select(item => (long)item).ToList();
            int numInitialToks = toks.Count;
            List<long> atn_mask = toks.Select(item => item != 0 ? 1L : 0L).ToList();

            //prepare array for handling output of network, size = output size,
            //  which corrosponds to one ind for each token id
            // see netron, this should match the output tensor size
            float[] latestLogits = new float[50257];

            //array for inputs
            NamedOnnxValue[] input = new NamedOnnxValue[2];

            //loop for max length           
            for (int i = 0; i < gen.max_length; ++i)
            {
                //tensors for input
                var inputTensor = new DenseTensor<long>(toks.ToArray(), new int[] { 1, toks.Count });
                var atnTensor = new DenseTensor<long>(atn_mask.ToArray(), new int[] { 1, atn_mask.Count });

                //inputs for session                
                input[0] = NamedOnnxValue.CreateFromTensor<long>("input_ids", inputTensor);
                input[1] = NamedOnnxValue.CreateFromTensor<long>("attention_mask", atnTensor);

                using (var output = inferenceSession!.Run(input).ToList().Last())
                {

                    var logits = output.Value as DenseTensor<float>;

                    //copy the output logits, shape is (batch) (sequence) (hidden layers logit output)
                    //see netron , or the specific model to figure out what the outputs are
                    int a = 0, b = logits!.Dimensions[1] - 1;
                    for (int c = 0; c < logits.Dimensions[2]; c++)
                    {
                        latestLogits[c] = logits[new int[] { a, b, c }];
                    }

                    int selectedToken = -1;

                    //we can avoid some stuff if topk == 1
                    if (gen.use_topk && gen.top_k == 1)
                    {
                        float curmax = -999.0f;

                        for (int ind = 0; ind < latestLogits.Length; ++ind)
                        {
                            if (latestLogits[ind] > curmax)
                            {
                                curmax = latestLogits[ind];
                                selectedToken = ind;
                            }
                        }
                    }

                    //if we have no token, do stuff
                    if(selectedToken == -1)
                    {
                        //var logProbs = Softmax(latestLogits);
                        // lets do it in place, save ourselves creating a new array
                        InPlaceSoftmax(ref latestLogits);
                        ref float[] logProbs = ref latestLogits; //lazy way to alias the array

                        //apply temp
                        if (gen.temperature > 0)
                        {
                            for (int tempInd = 0; tempInd < logProbs.Length; tempInd++)
                            {
                                logProbs[tempInd] /= gen.temperature;
                            }
                        }

                        //convert this to a terrible list<ValueTuple<int,float>) for laziness
                        // int = inital index which is the token id
                        // val is the probability
                        var valList = ToDescendingIntFloatTupleList(logProbs);

                        //do top K
                        if (gen.use_topk && gen.top_k >= 1)
                        {
                            valList = TopKResults(valList, gen.top_k);
                        }

                        //get top p
                        if (gen.use_topp && gen.top_p > 0.0)
                        {
                            valList = TopPResults(valList, gen.top_p);
                        }

                        //select a random entry, based on value of its probability
                        selectedToken = SelectByProbability(valList).Item1;
                    }

                    //append token
                    toks.Add(selectedToken);

                    //append mask
                    atn_mask.Add(1);
                }
            }

            if (gen.removeInitialPrompt)
            {
                toks = toks.Take(new Range(numInitialToks, toks.Count)).ToList();
            }

            int[] tokArray = toks.Select(item => (int)item).ToArray();
            string text = TokenIdsToText(ref tokArray);

            return new List<string>() { text };
        }

    }
}
