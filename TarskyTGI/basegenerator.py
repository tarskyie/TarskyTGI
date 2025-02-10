from tracemalloc import stop
import llama_cpp
import sys
import json

model = None

def continue_text(input_text):
    with open('basestuff.json', 'r') as f:
        data = json.load(f)

    max_tokens = data['n_predict']
    temperature = 0.8
    top_p = 0.95
    min_p = 0.05
    typical_p = 1
    output = model(input_text, max_tokens=max_tokens, temperature=temperature, top_p=top_p, min_p=min_p, typical_p=typical_p)["choices"][0]["text"]
    return output

def load_model(model_path):
    global model
    try:
        model = llama_cpp.Llama(model_path=model_path, n_gpu_layers=30)
        print("$model_loaded$", flush=True)
    except Exception as e:
        print(f"$model_load_error$:{str(e)}", flush=True)

while True:
    cmd = input().strip()
    if cmd == "load":
        mod = input().strip()
        load_model(mod)
    elif cmd == "chat":
        ch = input().strip()
        if model is not None:
            try:
                response = continue_text(ch)
                response = response.replace("\n", "\\n")
                print(f"$response$:{response}", flush=True)
            except Exception as e:
                print(f"$error$:{str(e)}", flush=True)
        else:
            print("$not_loaded$", flush=True)
        #print("$generation_stop$")
    elif cmd == "exit":
        break
    sys.stdout.flush()