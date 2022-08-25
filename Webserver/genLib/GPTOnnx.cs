using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using BlingFire;
using DTO;
using System.Diagnostics;

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

        private List<ValueTuple<int, float>> ToDescendingIntFloatTupleList(Memory<float> vals)
        {
            return vals.ToArray()
                .Select((val, index) => (Index: index, Value: val))
                .OrderByDescending(result => result.Value)
                .ToList();
        }

        public float GetMax(ref Memory<float> values)
        {
            float max = values.Span[0];
            foreach (float f in values.Span)
            {
                if (f > max)
                    max = f;
            }
            return max;
        }

        private void InPlaceSoftmax(ref Memory<float> values)
        {
            float maxVal = GetMax(ref values);
            float expSum = 0.0f;
            for(int i =0; i <  values.Span.Length; ++i)
            {
                values.Span[i] = (float)Math.Exp(values.Span[i] - maxVal);
                expSum += values.Span[i];
            }


            for (int i = 0; i < values.Length; i++)
                values.Span[i] /= expSum;
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

        public bool MatchedStopSequence(List<long> toks, List<long[]>? stopToks)
        {
            if (stopToks == null)
                return false;

            foreach (long[] stopSequence in stopToks)
            {
                int stopInd = stopSequence.Length - 1;
                int selectedToksInd = toks.Count - 1;

                if (stopInd > selectedToksInd)
                    continue;

                for (; stopInd >= 0 && selectedToksInd >= 0; stopInd--, selectedToksInd--)
                {
                    if (stopSequence[stopInd] == toks[selectedToksInd])
                    {
                        if (stopInd == 0)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }


        //TODO: figure out how to keep the tensors on the GPU and use TorchSharp ( or some other method ) of
        //      doing the softmax/beam search / etc on the GPU instead of CPU <-> GPU each iteration
        //TODO: beam search
        //TODO: add batches
        public List<string> DoInference(GPTGenerationSettings gen)
        {
            //tokenize
            int[] promptTokenIds = Tokenize(gen.prompt);

            //build stopping token lists
            List<long[]>? stoppingTokens = null;
            if(gen.stopping_criteria != null && gen.stopping_criteria.Length > 0)
            {
                stoppingTokens = new List<long[]>(gen.stopping_criteria.Length);
                foreach (string str in gen.stopping_criteria)
                {
                    stoppingTokens.Add(Tokenize(str).Select(item => (long)item).ToArray());
                }
            }

            //hard coded tokenIds for stopping
            if(gen.stopping_criteria_tokIds != null)
            {
                if(stoppingTokens == null)
                    stoppingTokens = new List<long[]>(gen.stopping_criteria_tokIds.Length);

                foreach(long tokId in gen.stopping_criteria_tokIds)
                    stoppingTokens.Add(new long[] { tokId });
            }

            //convert to long, make attention mask
            List<long> toks = promptTokenIds.Select(item => (long)item).ToList();
            int numInitialToks = toks.Count;
            List<long> atn_mask = toks.Select(item => item != 0 ? 1L : 0L).ToList();

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

                RunOptions options = new RunOptions();


                using (var output = inferenceSession!.Run(input).ToList().Last())
                {

                    var logits = output.Value as DenseTensor<float>;
                    //we just need the pile of floats at the end of the tensor                    
                    int length = logits!.Dimensions[2];
                    int startInd = logits.Buffer.Length - length;

                    Memory<float> logitsSpan = logits.Buffer.Slice(startInd, length);
                    
                    //debug, check to ensure we sliced the right palce
                    //int a = 0, b = logits!.Dimensions[1] - 1;
                    //for (int c = 0; c < logits.Dimensions[2]; c++)
                        //Debug.Assert(logitsSpan.Span[c] == logits[new int[] { a, b, c }]);
                    

                    int selectedToken = -1;

                    //we can avoid some stuff if topk == 1
                    if (gen.use_topk && gen.top_k == 1)
                    {
                        float curmax = -999.0f;

                        for (int ind = 0; ind < logitsSpan.Span.Length; ++ind)
                        {
                            if (logitsSpan.Span[ind] > curmax)
                            {
                                curmax = logitsSpan.Span[ind];
                                selectedToken = ind;
                            }
                        }
                    }

                    //if we have no token, do stuff
                    if(selectedToken == -1)
                    {
                        InPlaceSoftmax(ref logitsSpan);

                        //apply temp
                        if (gen.temperature > 0)
                        {
                            for (int tempInd = 0; tempInd < logitsSpan.Span.Length; tempInd++)
                            {
                                logitsSpan.Span[tempInd] /= gen.temperature;
                            }
                        }

                        //convert this to a terrible list<ValueTuple<int,float>) for laziness
                        // int = inital index which is the token id
                        // val is the probability
                        var valList = ToDescendingIntFloatTupleList(logitsSpan);

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

                    //check for stopping
                    if (MatchedStopSequence(toks, stoppingTokens))
                        break;

                }
            }

            //strip off the fed in prompt
            if (gen.removeInitialPrompt)
            {
                toks = toks.Take(new Range(numInitialToks, toks.Count)).ToList();
            }

            //convert back to text...
            int[] tokArray = toks.Select(item => (int)item).ToArray();
            string text = TokenIdsToText(ref tokArray);

            return new List<string>() { text };
        }

    }
}
