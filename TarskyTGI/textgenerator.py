from tracemalloc import stop
import llama_cpp
import sys
import json

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

def continue_text(input_text):
    data = load_json()

    max_tokens = data['n_predict']
    temperature = data['temperature']
    top_p = data['top_p']
    min_p = data['min_p']
    typical_p = data['typical_p']
    
    message = input_text
    msgs.append({"role": "user", "content": message})

    output = model.create_chat_completion(messages=msgs, temperature=temperature, top_p=top_p, min_p=min_p, typical_p=typical_p, max_tokens=max_tokens)
    msgs.append({"role":"assistant", "content":output["choices"][0]["message"]["content"].strip()})
    return output["choices"][0]["message"]["content"].strip()

def load_model(model_path, layers, cformat):
    global model
    try:
        data = load_json()
        n_ctx = data['n_ctx']
        model = llama_cpp.Llama(model_path=model_path, n_gpu_layers=layers, chat_format=cformat, n_ctx=n_ctx)
        print("$model_loaded$", flush=True)
    except Exception as e:
        print(f"$model_load_error$:{str(e)}", flush=True)

while True:
    cmd = input().strip()
    if cmd == "load":
        mod = input().strip()
        layers = int(input().strip())
        cformat = input().strip()
        load_model(mod, layers, cformat)

    elif cmd == "chat":
        ch = input().replace("/[newline]", "\n")
        if model is not None:
            try:
                response = continue_text(ch)
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
    
    elif cmd == "append":
        role = input()
        msg = input()
        msgs.append({"role":role, "content":msg})

    elif cmd == "exit":
        break
    sys.stdout.flush()