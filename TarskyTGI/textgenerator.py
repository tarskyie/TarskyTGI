from tracemalloc import stop
import llama_cpp
import sys
import json

model = None
msgs = [{"role":"system", "content":"You are a helpful, respectful and honest assistant. Always answer as helpfully as possible, while being safe.  Your answers should not include any harmful, unethical, racist, sexist, toxic, dangerous, or illegal content. Please ensure that your responses are socially unbiased and positive in nature. If a question does not make any sense, or is not factually coherent, explain why instead of answering something not correct. If you don't know the answer to a question, please don't share false information."}]

def continue_text(input_text):
    with open('chatstuff.json', 'r') as f:
        data = json.load(f)

    max_tokens = data['n_predict']
    temperature = data['temperature']
    top_p = data['top_p']
    min_p = data['min_p']
    typical_p = data['typical_p']
    
    message = input_text
    msgs.append({"role": "user", "content": message})

    output = model.create_chat_completion(messages=msgs, max_tokens=max_tokens, temperature=temperature, top_p=top_p, min_p=min_p, typical_p=typical_p)
    msgs.append({"role":"assistant", "content":output["choices"][0]["message"]["content"].strip()})
    return output["choices"][0]["message"]["content"].strip()
    #return input_text

def load_model(model_path):
    global model
    try:
        with open('chatstuff.json', 'r') as f:
            data = json.load(f)
        model = llama_cpp.Llama(model_path=model_path, n_gpu_layers=data['layers'], chat_format=data['format'])
        print("$model_loaded$", flush=True)
    except Exception as e:
        print(f"$model_load_error$:{str(e)}", flush=True)

while True:
    cmd = input().strip()
    if cmd == "load":
        mod = input().strip()
        load_model(mod)
    elif cmd == "chat":
        ch = input()
        if model is not None:
            try:
                response = continue_text(ch)
                response = response.replace("\n", "\\n")
                print(f"$response$:{response}", flush=True)
                #print(ch)
            except Exception as e:
                print(f"$error$:{str(e)}", flush=True)
        else:
            print("$not_loaded$", flush=True)
            #print(ch)
    elif cmd == "clear":
        msgs.clear()
        msgs.append({"role":"system", "content":"You are a helpful, respectful and honest assistant. Always answer as helpfully as possible, while being safe.  Your answers should not include any harmful, unethical, racist, sexist, toxic, dangerous, or illegal content. Please ensure that your responses are socially unbiased and positive in nature. If a question does not make any sense, or is not factually coherent, explain why instead of answering something not correct. If you don't know the answer to a question, please don't share false information."})
    elif cmd == "exit":
        break
    sys.stdout.flush()