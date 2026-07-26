FROM python:3.11-slim

# libsndfile1: cần cho librosa decode audio (webm/ogg/wav...)
RUN apt-get update && apt-get install -y --no-install-recommends \
    libsndfile1 \
    ffmpeg \
    git \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app.py .

# Model tự tải về HF cache lúc container start (xem @app.on_event("startup") trong app.py) —
# mount volume /root/.cache/huggingface ở docker-compose để không tải lại mỗi lần rebuild/restart.
EXPOSE 8001

CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8001"]
