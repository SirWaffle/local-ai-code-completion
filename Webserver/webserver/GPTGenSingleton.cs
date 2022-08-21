using DTO;
using genLib;
using Microsoft.AspNetCore.Mvc;
using PatternToolbox.DataStructures;

namespace webserver
{
    public interface IGeneratorSingleton
    {
        Task<List<string>?> DoInference(GPTRequest req);
    }


    public class GPTGenSingleton : IGeneratorSingleton
    {
        string tokenizerPath = @".\..\..\blingfireTokenizerModels\gpt2.bin";
        string detokenPath = @".\..\..\blingfireTokenizerModels\gpt2.i2w";

        string modelPath = @"E:\MLmodels\llm\gpt-neo-1.3B\onnx-casualLM\model.onnx";
        int gpuDeviceId = 0;

        int delayTimePerRequestMs = 500;

        private GPTOnnx gptGenerator = new();
        private SemaphoreSlim semaphore = new SemaphoreSlim(1);

        private QueueDictionaryLocking<GPTRequest> queueDict = new();

        public GPTGenSingleton()
        {
            gptGenerator.Load(gpuDeviceId, modelPath, tokenizerPath, detokenPath);
        }

        async public Task<List<string>?> DoInference(GPTRequest req)
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
                GPTRequest? queuedReq = null;
                queueDict.TryGetAndPopNextRequestForUserBlocking(req.username, ref queuedReq );

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
