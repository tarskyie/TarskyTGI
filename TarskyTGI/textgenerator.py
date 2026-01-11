from tracemalloc import stop
import llama_cpp
import sys
import json
import base64

# Ensure UTF-8 stdin/stdout
try:
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stdin.reconfigure(encoding='utf-8')
except Exception:
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace', line_buffering=True)
    sys.stdin = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8', errors='replace')

model = None
msgs = []


def load_json():
    with open('chat.json', 'r') as f:
        data = json.load(f)
    return data


def image_to_base64_data_uri(img_path):
    with open(img_path, "rb") as img_file:
        base64_data = base64.b64encode(img_file.read()).decode('utf-8')
        return f"data:image/png;base64,{base64_data}"


def continue_text(input_text, img=None):
    data = load_json()

    max_tokens = data.get('n_predict')
    temperature = data.get('temperature')
    top_p = data.get('top_p')
    min_p = data.get('min_p')
    typical_p = data.get('typical_p')

    message = input_text

    # llava-style messages may include image as structured content
    if img and img != "None":
        data_uri = image_to_base64_data_uri(img)
        msgs.append({"role": "user", "content": [{"type":"text", "text":message}, {"type": "image_url", "image_url": {"url": data_uri } }]})
    else:
        msgs.append({"role": "user", "content": message})

    # Try to call create_chat_completion with available params
    try:
        # Some models use chat parameters explicitly
        output = model.create_chat_completion(messages=msgs, temperature=temperature, top_p=top_p, min_p=min_p, typical_p=typical_p)
    except TypeError:
        # Fallback if model API differs
        output = model.create_chat_completion(messages=msgs)

    # Extract assistant content depending on API shape
    try:
        assistant_content = output["choices"][0]["message"]["content"].strip()
    except Exception:
        # fallback: try output.choices[0].message.content
        try:
            assistant_content = output.choices[0].message.content.strip()
        except Exception:
            assistant_content = str(output)

    msgs.append({"role":"assistant", "content":assistant_content})
    return assistant_content


def load_model_text(model_path, layers, cformat):
    global model
    try:
        data = load_json()
        n_ctx = data.get('n_ctx')
        # older textgenerator used chat_format param
        model = llama_cpp.Llama(model_path=model_path, n_gpu_layers=layers, chat_format=cformat, n_ctx=n_ctx)
        print("$model_loaded$", flush=True)
    except Exception as e:
        print(f"$model_load_error$:{str(e)}", flush=True)


def load_model_llava(model_path, layers, cformat, mmproj, ctx):
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


# Main loop: accept both legacy (text) and llava load patterns.
while True:
    try:
        cmd = input().strip()
    except EOFError:
        break

    if cmd == "load":
        # read next token and decide whether it's layers (int) or mmproj (string)
        mod = input().strip()
        second = input().strip()
        # if second is an integer -> text mode: second is layers
        try:
            layers = int(second)
            # then next is chat format
            cformat = input().strip()
            load_model_text(mod, layers, cformat)
        except ValueError:
            # llava mode: second is mmproj
            mmproj = second
            try:
                layers = int(input().strip())
                ctx = int(input().strip())
                cformat = input().strip()
            except Exception as e:
                print(f"$model_load_error$:{str(e)}", flush=True)
                continue
            load_model_llava(mod, layers, cformat, mmproj, ctx)

    elif cmd == "chat":
        # read message, then read image line (may be 'None')
        ch = input().replace("/[newline]", "\n")
        # attempt to read next line for image path; if none provided, assume None
        try:
            im = input().strip()
        except EOFError:
            im = "None"

        if model is not None:
            try:
                response = continue_text(ch, im)
                # escape newlines for C# consumer convention
                response = response.replace("\n", "/[newline]")
                print(f"$response$:{response}", flush=True)
            except Exception as e:
                print(f"$error$:{str(e)}", flush=True)
        else:
            print("$not_loaded$", flush=True)

    elif cmd == "chat_server":
        ch = input().replace("/[newline]", "\n")
        sprompt = input()
        msgs = [{"role":"system", "content":sprompt}]
        if model is not None:
            try:
                response = continue_text(ch)
                response = response.replace("\n", "/[newline]")
                print(f"$response$:{response}", flush=True)
            except Exception as e:
                print(f"$error$:{str(e)}", flush=True)
        else:
            print("$not_loaded$", flush=True)

    elif cmd == "clear":
        msgs.clear()
        msgs.append({"role":"system", "content":"You are a helpful, respectful and honest assistant. Always answer as helpfully as possible, while being safe.  Your answers should not include any harmful, unethical, racist, sexist, toxic, dangerous, or illegal content. Please ensure that your responses are socially unbiased and positive in nature. If a question does not make any sense, or is not factually coherent, explain why instead of answering something not correct. If you don't know the answer to a question, please don't share false information."})

    elif cmd == "append":
        role = input()
        msg = input()
        msgs.append({"role":role, "content":msg})

    elif cmd == "exit":
        break

    sys.stdout.flush()