namespace FengDeskAI.Domain.Enums;

/// <summary>Nguồn đăng ký/đăng nhập ban đầu của user. Không chặn user login bằng cách khác nếu đã link (vd GoogleId đã gắn).</summary>
public enum AuthProvider
{
    Local = 0,
    Google = 1,
}
