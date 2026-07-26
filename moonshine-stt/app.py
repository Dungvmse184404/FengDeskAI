"""
Moonshine STT — microservice nội bộ, giả lập endpoint OpenAI /v1/audio/transcriptions
(cùng hình dạng response với "speaches" đang phục vụ Whisper) để BE .NET (ISpeechToTextService)
gọi được mà không cần đổi contract.

Khác với Whisper (1 model đa ngôn ngữ, tự nhận diện): Moonshine train RIÊNG 1 model / ngôn ngữ,
nên KHÔNG tự nhận diện được câu nói trộn Việt-Anh. FE phải tự chọn ngôn ngữ (nút VI/EN có sẵn ở
useSpeechInput) và gửi field "model" tương ứng — service này chỉ việc route theo tên model.

Model tải sẵn khi container khởi động (KHÔNG tải lười theo request) để tránh độ trễ lần gọi đầu.
"""

import io
import logging

import moonshine_onnx as moonshine
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from fastapi.responses import JSONResponse

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("moonshine-stt")

app = FastAPI(title="Moonshine STT", version="1.0.0")

# Tên model client gửi lên (field "model") -> tên model thật trong package moonshine_onnx.
# moonshine/tiny, moonshine/base (EN) CHẮC CHẮN tải tự động qua HF hub (có trong ví dụ chính thức).
# moonshine/tiny-vi CHƯA XÁC NHẬN được là tải tự động qua đúng tên này — repo HF thật của bản Việt
# là "UsefulSensors/moonshine-tiny-vi" (gạch nối, khác format "moonshine/xxx-lang" mà package dùng
# cho bản Hàn "moonshine/tiny-ko"). Nếu chạy service này mà lỗi tải model "moonshine/tiny-vi",
# cần tải thủ công repo UsefulSensors/moonshine-tiny-vi rồi dùng:
#   MoonshineOnnxModel(models_dir="/path/to/moonshine-tiny-vi")
# thay cho việc truyền thẳng string tên model. Xem app.py TODO bên dưới nếu gặp lỗi này.
MODEL_ALIASES = {
    "moonshine/tiny-vi": "moonshine/tiny-vi",
    "moonshine/tiny": "moonshine/tiny",
    "moonshine/base": "moonshine/base",
}

# Preload — moonshine_onnx tự cache theo tên model trong lần transcribe() đầu tiên; ở đây ta chủ
# động "làm nóng" cả 2 model lúc startup bằng 1 audio rỗng ngắn để tránh cold-start lúc user đầu
# tiên bấm mic.
_loaded_models: set[str] = set()


@app.on_event("startup")
def _warmup() -> None:
    import numpy as np

    silence = np.zeros(16000, dtype=np.float32)  # 1s im lặng, 16kHz mono — input chuẩn Moonshine
    for alias, real_name in MODEL_ALIASES.items():
        try:
            moonshine.transcribe(silence, real_name)
            _loaded_models.add(alias)
            logger.info("Đã nạp model %s", real_name)
        except Exception:
            logger.exception("Nạp model %s thất bại — sẽ thử lại khi có request đầu tiên", real_name)


def _decode_audio_to_16k_mono(raw: bytes):
    """Audio từ FE là webm/ogg (MediaRecorder) — decode + resample về 16kHz mono float32 bằng librosa."""
    import librosa
    import numpy as np

    audio, _sr = librosa.load(io.BytesIO(raw), sr=16000, mono=True)
    return audio.astype(np.float32)


@app.get("/health")
def health():
    return {"status": "ok", "loaded_models": sorted(_loaded_models)}


@app.post("/v1/audio/transcriptions")
async def transcribe(
    file: UploadFile = File(...),
    model: str = Form("moonshine/tiny-vi"),
    response_format: str = Form("json"),
):
    real_name = MODEL_ALIASES.get(model)
    if real_name is None:
        raise HTTPException(
            status_code=400,
            detail=f"Model '{model}' không hỗ trợ. Hợp lệ: {list(MODEL_ALIASES)}",
        )

    raw = await file.read()
    if not raw:
        raise HTTPException(status_code=400, detail="File audio rỗng.")

    try:
        audio = _decode_audio_to_16k_mono(raw)
        text = moonshine.transcribe(audio, real_name)
        # moonshine_onnx.transcribe trả list[str] (1 phần tử cho input đơn) hoặc str tùy version —
        # chuẩn hóa về string để khớp response {"text": "..."} mà WhisperSpeechToTextService đang parse.
        if isinstance(text, list):
            text = " ".join(text)
    except Exception:
        logger.exception("Transcribe lỗi (model=%s)", real_name)
        raise HTTPException(status_code=500, detail="Lỗi nhận diện giọng nói.")

    return JSONResponse({"text": (text or "").strip()})
