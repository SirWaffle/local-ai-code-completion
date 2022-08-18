namespace DTO
{
    public class GPTGenerationSettings
    {
        public string prompt { get; set; } = String.Empty;
        public bool use_topk { get; set; } = false;
        public bool use_topp { get; set; } = true;
        public float top_p { get; set; } = 0.8f; //0.8f
        public int top_k { get; set; } = 50; //5, 50;
        public float temperature { get; set; } = 0.8f; //0.8f
        public int max_length { get; set; } = 50;
        public int return_sequences { get; set; } = 1;
        public string[]? stopping_criteria { get; set; } = null;//new string[] { "\n","\n\n", @"\n", @"\n\n" }

        public bool removeInitialPrompt { get; set; } = true;

    }
}