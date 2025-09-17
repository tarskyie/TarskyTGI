import os
import argparse
import asyncio
from fastapi import FastAPI
from pydantic import BaseModel
from typing import List, Optional
import uvicorn

# === Replace this with your model integration ===
# Example: llama-cpp-python
try:
    from llama_cpp import Llama
except ImportError:
    Llama = None
    print("Warning: llama-cpp-python not installed. Install with `pip install llama-cpp-python fastapi uvicorn`.")

# -------------------------
# OpenAI API compatible schema
# -------------------------
class ChatMessage(BaseModel):
    role: str
    content: str

class ChatRequest(BaseModel):
    model: str
    messages: List[ChatMessage]
    max_tokens: Optional[int] = 256
    temperature: Optional[float] = 0.7
    top_p: Optional[float] = 0.95

class ChatResponseChoice(BaseModel):
    index: int
    message: ChatMessage
    finish_reason: str

class ChatResponse(BaseModel):
    id: str
    object: str
    created: int
    model: str
    choices: List[ChatResponseChoice]

# -------------------------
# FastAPI app
# -------------------------
app = FastAPI()
llm = None

@app.post("/v1/chat/completions", response_model=ChatResponse)
async def create_chat_completion(request: ChatRequest):
    if llm is None:
        return {"error": "Model not loaded."}

    # Build prompt from messages
    prompt = ""
    for msg in request.messages:
        prompt += f"{msg.role}: {msg.content}\n"
    prompt += "assistant:"

    # Run inference
    output = llm(
        prompt,
        max_tokens=request.max_tokens,
        temperature=request.temperature,
        top_p=request.top_p,
        stop=["user:", "assistant:"]
    )

    text = output["choices"][0]["text"].strip()

    # Response in OpenAI format
    return ChatResponse(
        id="chatcmpl-1",
        object="chat.completion",
        created=int(asyncio.get_event_loop().time()),
        model=request.model,
        choices=[
            ChatResponseChoice(
                index=0,
                message=ChatMessage(role="assistant", content=text),
                finish_reason="stop"
            )
        ]
    )

# -------------------------
# Entrypoint
# -------------------------
if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", type=str, required=True, help="Path to GGUF or model file")
    parser.add_argument("--ctx-size", type=int, default=1024)
    parser.add_argument("--n-gpu-layers", type=int, default=0)
    parser.add_argument("--host", type=str, default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8080)
    args = parser.parse_args()

    if Llama is None:
        raise RuntimeError("llama-cpp-python not available. Please install it.")

    print(f"Loading model: {args.model}")
    llm = Llama(
        model_path=args.model,
        n_ctx=args.ctx_size,
        n_gpu_layers=args.n_gpu_layers,
        verbose=False
    )

    uvicorn.run(app, host=args.host, port=args.port)
