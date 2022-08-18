using DTO;
using genLib;
using Microsoft.AspNetCore.Mvc;

namespace webserver
{
    public interface IGeneratorSingleton
    {
        Task<List<string>> DoInference(GPTGenerationSettings gen);
    }


    public class GPTGenSingleton : IGeneratorSingleton
    {
        string tokenizerPath = @".\..\..\blingfireTokenizerModels\gpt2.bin";
        string detokenPath = @".\..\..\blingfireTokenizerModels\gpt2.i2w";

        string modelPath = @"E:\MLmodels\llm\gpt-neo-1.3B\onnx-casualLM\model.onnx";
        int gpuDeviceId = 0;

        private GPTOnnx gptGenerator = new();
        private SemaphoreSlim semaphore = new SemaphoreSlim(1);

        public GPTGenSingleton()
        {
            gptGenerator.Load(gpuDeviceId, modelPath, tokenizerPath, detokenPath);
        }

        async public Task<List<string>> DoInference(GPTGenerationSettings gen)
        {
            List<string> results = new();

            try
            {
                await semaphore.WaitAsync();
                results = gptGenerator.DoInference(gen);
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
