# local-ai-code-completion
Local code completion example, using ONNX, GPT Neo, with a c# webserver, VSCode and Visual Studio extensions


# Currently a work in progress!

this is just going to be a least code require, simple example that connects everything together to make your own code completion / code generator.

lots of things are not effecient, or just plain bad, or hard coded - but a good jumping off point. I will improve stuff as i have time


# roadmap
- working on making an easily run VSCode extension
- Visual Studio suggestion extension
- more models for generation, especially code centric models
- huggingfaces repositories of ONNX exported models
- better behavior of plugins ( caching, etc )
- better management of generation
- better how to guide


# bad code ( should be changed! )
- webserver only allows one generation of the model at a time, enforced by a semaphor
- hard coded paths to model locations and what not


# rough directions
- Clone gpt-neo 1.3B ( https://huggingface.co/EleutherAI/gpt-neo-1.3B )

- Use these instructions to export a model via python scripts: https://huggingface.co/docs/transformers/serialization

- I ran this in the same directory as the checkout of the model, which put the ONNX model in the onnx/ dir:
```
python -m transformers.onnx --model=. --feature=causal-lm onnx/
```

- use netron to view your model, observe the inputs and outputs. THe input names of the model should match in GPTOnnx.cs: https://netron.app/

- edit the paths on the webserver to point to your model: \Webserver\webserver\GPTGenSingleton.cs

- i run this via the cuda execution of ONNX, which requires about 8 GB free of VRAM. If you want to change this to CPU, see: \Webserver\genLib\GPTOnnx.cs
```
and comment out:
so.AppendExecutionProvider_CUDA(gpuDeviceId);
```
- run web server

- run web server test program, which will call the web server, generate text, and print to screen

- run the vscode extension project, and test it out in VSCode!

- (more to come as I get things farther along )
