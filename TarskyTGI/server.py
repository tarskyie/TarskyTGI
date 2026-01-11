import os
import argparse
import asyncio
from fastapi import FastAPI
from pydantic import BaseModel
from typing import List, Optional
from fastapi import Header, HTTPException
import sys
import uvicorn
import time

try:
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stdin.reconfigure(encoding='utf-8')
except Exception:
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace', line_buffering=True)
    sys.stdin = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8', errors='replace')

try:
    from llama_cpp import Llama
except ImportError:
    Llama = None
    print("Warning: llama-cpp-python not installed. Install with `pip install llama-cpp-python fastapi uvicorn`.")

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
api_key="none"

@app.post("/v1/chat/completions", response_model=ChatResponse)
async def create_chat_completion(request: ChatRequest, authorization: str = Header(None)):
    if authorization != f"Bearer {api_key}" and api_key != "none":
        raise HTTPException(status_code=401, detail="Invalid or missing API key")

    if llm is None:
        return {"error": "Model not loaded."}

    # Build prompt from messages
    msgs = [m.dict() for m in request.messages]

    # Run inference
    output = llm.create_chat_completion(messages=msgs, temperature=request.temperature, top_p=request.top_p, max_tokens=request.max_tokens)

    choice = output["choices"][0]
    llm.reset()
    # Response in OpenAI format
    return ChatResponse(
        id=output.get("id", "chatcmpl-1"),
        object=output.get("object", "chat.completion"),
        created=int(time.time()),
        model=request.model,
        choices=[
            ChatResponseChoice(
                index=choice["index"],
                message=choice["message"],
                finish_reason=choice.get("finish_reason", "stop")
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
    parser.add_argument("--format", type=str, default="llama-3")
    parser.add_argument("--key", type=str, default="none")
    args = parser.parse_args()

    if Llama is None:
        raise RuntimeError("llama-cpp-python not available. Please install it.")

    print(f"Loading model: {args.model}")
    llm = Llama(
        model_path=args.model,
        n_ctx=args.ctx_size,
        n_gpu_layers=args.n_gpu_layers,
        verbose=False,
        chat_format=args.format
    )

    api_key = args.key

    uvicorn.run(app, host=args.host, port=args.port)
