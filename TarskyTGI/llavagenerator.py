from tracemalloc import stop
import llama_cpp
import sys
import json
import base64

model = None
msgs = [{"role":"system", "content":"You are an assistant who perfectly describes images."}]

def load_json():
    with open('chatstuff.json', 'r') as f:
            data = json.load(f)
    return data

def image_to_base64_data_uri(img_path):
    with open(img_path, "rb") as img_file:
        base64_data = base64.b64encode(img_file.read()).decode('utf-8')
        return f"data:image/png;base64,{base64_data}"

def continue_text(input_text, img):
    data = load_json()

    max_tokens = data['n_predict']
    temperature = data['temperature']
    top_p = data['top_p']
    min_p = data['min_p']
    typical_p = data['typical_p']

    message = input_text
    if img != "None":
        data_uri = image_to_base64_data_uri(img)
        msgs.append({"role": "user", "content": [{"type":"text", "text":message}, {"type": "image_url", "image_url": {"url": data_uri } }]})
    else:
        msgs.append({"role": "user", "content": [{"type":"text", "text":message}]})

    output = model.create_chat_completion(messages=msgs)
    msgs.append({"role":"assistant", "content":output["choices"][0]["message"]["content"].strip()})
    return output["choices"][0]["message"]["content"].strip()

def load_model(model_path, layers, cformat, mmproj, ctx):
    global model
    try:
        if cformat == "llava-1-5":
            chat_handler = llama_cpp.llama_chat_format.Llava15ChatHandler(clip_model_path=mmproj)
        elif cformat == "llava-1-6":
            chat_handler = llama_cpp.llama_chat_format.Llava16ChatHandler(clip_model_path=mmproj)
        elif cformat == "moondream2":
            chat_handler = llama_cpp.llama_chat_format.MoondreamChatHandler(clip_model_path=mmproj)
        elif cformat == "nanollava":
            chat_handler = llama_cpp.llama_chat_format.NanoLlavaChatHandler(clip_model_path=mmproj)
        elif cformat == "llama-3-vision-alpha":
            chat_handler = llama_cpp.llama_chat_format.Llama3VisionAlphaChatHandler(clip_model_path=mmproj)
        elif cformat == "minicpm-v-2.6":
            chat_handler = llama_cpp.llama_chat_format.MiniCPMv26ChatHandler(clip_model_path=mmproj)
        elif cformat == "obsidian":
            chat_handler = llama_cpp.llama_chat_format.ObsidianChatHandler(clip_model_path=mmproj)
        else:
            print("$model_load_error$:incorrect cformat", flush=True)
            return
        model = llama_cpp.Llama(model_path=model_path, chat_handler=chat_handler, n_ctx=ctx, n_gpu_layers=layers)
        print("$model_loaded$", flush=True)
    except Exception as e:
        print(f"$model_load_error$:{str(e)}", flush=True)

while True:
    cmd = input().strip()
    if cmd == "load":
        mod = input().strip()
        mmproj = input().strip()
        layers = int(input().strip())
        ctx = int(input().strip())
        cformat = input().strip()
        load_model(mod, layers, cformat, mmproj, ctx)
    elif cmd == "chat":
        ch = input()
        im = input().strip()
        if model is not None:
            try:
                response = continue_text(ch, im).replace("\n", "\\n")
                print(f"$response$:{response}", flush=True)
            except Exception as e:
                print(f"$error$:{str(e)}", flush=True)
        else:
            print("$not_loaded$", flush=True)
    elif cmd == "clear":
        msgs.clear()
        msgs.append({"role":"system", "content":"You are a helpful, respectful and honest assistant. Always answer as helpfully as possible, while being safe.  Your answers should not include any harmful, unethical, racist, sexist, toxic, dangerous, or illegal content. Please ensure that your responses are socially unbiased and positive in nature. If a question does not make any sense, or is not factually coherent, explain why instead of answering something not correct. If you don't know the answer to a question, please don't share false information."})
    elif cmd == "exit":
        break
    sys.stdout.flush()