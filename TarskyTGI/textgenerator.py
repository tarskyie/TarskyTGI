from tracemalloc import stop
import llama_cpp
import sys

model = None

def continue_text(input_text):
    max_tokens = 200
    temperature = 0.8
    top_p = 0.95
    min_p = 0.05
    typical_p = 1
    stoplist=["[newline]User:", "[newline]Assistant:"]
    output = model(input_text, max_tokens=max_tokens, temperature=temperature, top_p=top_p, min_p=min_p, typical_p=typical_p, stop=stoplist)["choices"][0]["text"]
    return output

def load_model(model_path):
    global model
    try:
        model = llama_cpp.Llama(model_path=model_path)
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
                print(f"$response$:{response}", flush=True)
            except Exception as e:
                print(f"$error$:{str(e)}", flush=True)
        else:
            print("$not_loaded$", flush=True)
    elif cmd == "exit":
        break
    sys.stdout.flush()