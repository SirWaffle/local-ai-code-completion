using airtist_webserver.DataStructures;
using DTO;
using genLib.Generators;
using Microsoft.AspNetCore.Mvc;

namespace airtist_webserver.Generators
{
    public interface IGeneratorSingleton
    {
        Task<List<string>?> DoInference(CodeGenRequest req);
    }

    //TODO: figure out how to get proper tokenization for the codegen models
    //      requires figuring out how to compile blingfire models and what not 
    public class CodeGenSingleton : IGeneratorSingleton
    {
        /*
        // these work with the BlingFireTokenizer:
        
        string tokenizerPath = @".\..\..\blingfireTokenizerModels\gpt2.bin";
        string detokenPath = @".\..\..\blingfireTokenizerModels\gpt2.i2w";

        string modelPath = @"E:\MLmodels\llm\gpt-neo-1.3B\onnx-casualLM\model.onnx";
        */

        // heres a code centric model, onnx, using a huggingfaces tokenizer interop
        string tokenizerPath = @"E:\MyHFModels\codegen-350M-multi-onnx\tokenizer.json";
        string modelPath = @"E:\MyHFModels\codegen-350M-multi-onnx\model.onnx";
        //string modelPath = @"E:\MLModels\codegen\codegen-2B-multi\onnx-causalLM\model.onnx";

        //gpu device to use, if there are multiple GPU's available
        int gpuDeviceId = 0;


        //how long to wait before processing a request...
        //a slight delay lets us get a queue of requests, which we can cancel multiple requests
        //from the sameuser, and only use the latest.
        //quick, hacky way to deal with constant flood of requests for each keypress
        //and shares that logic between the VSCode extension and the visuals tudio extension
        int delayTimePerRequestMs = 500;

        private CodeGenOnnx gptGenerator = new();
        private SemaphoreSlim semaphore = new SemaphoreSlim(1);

        private QueueDictionaryLocking<CodeGenRequest> queueDict = new();

        public CodeGenSingleton()
        {
            Console.WriteLine("LOADING MODEL");
            // if we want to use GPT and blingfire
            // genLib.Tokenizers.BlingFireTokenizer tok = new(tokenizerPath, detokenPath);

            //lets make a huggign face code centric model...
            genLib.Tokenizers.HFTokenizer tok = new(tokenizerPath);

            gptGenerator.Load(gpuDeviceId, modelPath, tok);
            Console.WriteLine("MODEL LOADED");
        }

        async public Task<List<string>?> DoInference(CodeGenRequest req)
        {
            List<string>? results = null;

            try
            {
                //enqueue the request
                Console.WriteLine("QUEUED");
                queueDict.QueueRequestBlocking(req.username, req);

                //delay before we try to gen, this allows rapid fire requeust to queue up
                //so that we can start dropping older, non applicable generations
                Console.WriteLine("DELAYING");
                await Task.Delay(delayTimePerRequestMs);

                Console.WriteLine("WAITING FOR GEN");

                //wait until gen is ready for the next item
                await semaphore.WaitAsync();

                //dequeue next request for this user
                CodeGenRequest? queuedReq = null;
                queueDict.TryGetAndPopNextRequestForUserBlocking(req.username, ref queuedReq);

                //we only want to generate the latest request for this user, everything else can be dropped
                int count = queueDict.GetRequestCount(req.username);
                if (count > 0)
                {
                    Console.WriteLine("DUMPING, PENDING QUEUE FOR USER: " + count);
                    //if we have more in queue, let this one go without doing anything
                }
                else
                {
                    Console.WriteLine("INFERENCING");

                    //go ahead and generate, using the requested prompt from the last spot in the queue
                    results = gptGenerator.DoInference(queuedReq!.generationSettings);
                    Console.WriteLine("INFERENCING COMPLETE");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("EXCEPTION: " + ex);
            }

            semaphore.Release();
            return results;
        }
    }
}
