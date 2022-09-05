# local-ai-code-completion
Local code completion example, using ONNX models, Codegen model ( or whatever other model ), with a c# webserver, VSCode and Visual Studio extensions, to get similar copilot code completion behavior, at the symbol, line, or full method level, running on your own machine!


## more info at githubs/etc
- codegen model: https://github.com/salesforce/CodeGen
- huggingface codegen info: https://huggingface.co/docs/transformers/main/en/model_doc/codegen
- huggingface tokenizers: https://github.com/huggingface/tokenizers
- blingfire: https://github.com/microsoft/BlingFire


## Currently a work in progress!

this is mostly a least code require, simple example that connects everything together to make your own code completion / code generator, meant to run locally with just one user ( but does support multiple users ). Extending it to act as a service for multiple users will work, but there are inefficiencies and whatnot that should be addressed for that to work well 

This should act as a good jumping off point to customize to your needs, and I will improve stuff as I have time


## roadmap / TODO
- working on making an easily run VSCode extension ( currently i just launch via vscode extension debug )
- Visual Studio suggestion extension
- better behavior of plugins ( caching, etc )
- better management of generation
- better how to guide
- Adding beam search, and multiple samples to the generation code
- making post processing steps ( searches, softmax, top_k, top_p ) operate in GPU to avoid costly CPU <-> GPU copies of tensors


## bad code ( should be changed! )
- webserver only allows one generation of the model at a time, enforced by a semaphor
- hard coded paths to model locations and what not in C#, not json files

## premade ONNX model
I put an ONNX model and the required tokenizer.json file up on huggingface.  if the model isnt there yet, im probably still uploading... :

- https://huggingface.co/SirWaffle/codegen-350M-multi-onnx/tree/main

## rough directions
- Webserver is written in visual studio 2022. Open the solution, and build and run the webserver.

- edit the paths on the webserver to point to your model, and token.json file: \Webserver\webserver\Generators\CodeGenSingleton.cs

- i run this via the cuda execution of ONNX, which requires about 8 GB free of VRAM. If you want to change this to CPU, see: \Webserver\genLib\Generators\CodeGenOnnx.cs
```
and comment out:
so.AppendExecutionProvider_CUDA(gpuDeviceId);
```
- run web server

- run web server test program, which will call the web server, generate text, and print to screen

- run the vscode extension project, and test it out in VSCode!

- (more to come as I get things farther along )

## to make your own ONNX model

- Clone the model, for example, gpt-neo 1.3B ( https://huggingface.co/EleutherAI/gpt-neo-1.3B ), or any of the codegen model

- Use these instructions to export a model via python scripts: https://huggingface.co/docs/transformers/serialization

- I ran this in the same directory as the checkout of the model, which put the ONNX model in the onnx/ dir. Same command applies for codegen models:
```
python -m transformers.onnx --model=. --feature=causal-lm onnx/
```

- use netron to view your model, observe the inputs and outputs. The input names of the model should match in CodeGenOnnx.cs: https://netron.app/