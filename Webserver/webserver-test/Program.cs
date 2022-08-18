using System.Net.Http.Headers;
using System.Net.Http.Json;
using DTO;



GPTRequest req = new();
req.username = "username";
req.generationSettings.prompt = "<| file ext=.cpp |>" + Environment.NewLine + "int main() { for(int i = 0; i < 10;";

//for code completion, always pick the #1 best predicted token ( its also faster )
req.generationSettings.use_topk = true;
req.generationSettings.top_k = 1;

req.generationSettings.use_topp = false;




HttpClient client = new HttpClient();

client.BaseAddress = new Uri("http://localhost:5184/");
client.DefaultRequestHeaders.Accept.Clear();
client.DefaultRequestHeaders.Accept.Add(
    new MediaTypeWithQualityHeaderValue("application/json"));


HttpResponseMessage response = await client.PostAsJsonAsync(
    $"gpt/generate", req);
response.EnsureSuccessStatusCode();

// Deserialize the updated product from the response body.
GPTResponse? resp = await response.Content.ReadFromJsonAsync<GPTResponse>();

Console.WriteLine(String.Join(Environment.NewLine + Environment.NewLine, resp!.reponseText!));
Console.ReadLine();
