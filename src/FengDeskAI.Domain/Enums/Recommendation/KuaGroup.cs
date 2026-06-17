namespace FengDeskAI.Domain.Enums.Recommendation;

/// <summary>
/// Nhóm cung mệnh theo Kua number — quyết định tập hướng tốt.
/// Đông tứ mệnh: hướng tốt {Bắc, Nam, Đông, Đông Nam}.
/// Tây tứ mệnh: hướng tốt {Tây, Tây Bắc, Tây Nam, Đông Bắc}.
/// </summary>
public enum KuaGroup
{
    East, // Đông tứ mệnh
    West, // Tây tứ mệnh
}
