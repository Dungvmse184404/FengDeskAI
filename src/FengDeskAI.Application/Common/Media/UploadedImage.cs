namespace FengDeskAI.Application.Common.Media;

/// <summary>Một file ảnh nhận từ request multipart — tầng WebAPI mở stream, service chỉ đọc.</summary>
public sealed record UploadedImage(Stream Content, string FileName, string ContentType);
