namespace FengDeskAI.Application.Features.Shipping.Services;

/// <summary>
/// Tính phí vận chuyển deterministic cho một delivery (vận đơn của 1 store).
/// Theo vùng (nội tỉnh / liên tỉnh) + bậc cân nặng. Thuần logic, không gọi DB/ngoài.
/// </summary>
public interface IShippingFeeCalculator
{
    /// <summary>
    /// Phí ship cho một delivery. <paramref name="originProvinceId"/> null (store chưa có địa chỉ)
    /// → coi như liên tỉnh (phí cao hơn).
    /// </summary>
    decimal Calculate(Guid? originProvinceId, Guid? destProvinceId, int totalWeightGram);
}

public class ShippingFeeCalculator : IShippingFeeCalculator
{
    // Hằng số demo — chỉnh tự do.
    private const decimal BaseSameProvince = 15_000m;  // nội tỉnh, 1kg đầu
    private const decimal BaseCrossProvince = 30_000m; // liên tỉnh, 1kg đầu
    private const int FirstWeightGram = 1_000;         // gói trong 1kg đầu
    private const int StepGram = 500;                  // mỗi 0.5kg vượt
    private const decimal StepFee = 5_000m;

    public decimal Calculate(Guid? originProvinceId, Guid? destProvinceId, int totalWeightGram)
    {
        var sameProvince = originProvinceId.HasValue
            && destProvinceId.HasValue
            && originProvinceId.Value == destProvinceId.Value;

        var baseFee = sameProvince ? BaseSameProvince : BaseCrossProvince;

        var extra = Math.Max(0, totalWeightGram - FirstWeightGram);
        var steps = (int)Math.Ceiling(extra / (double)StepGram);
        return baseFee + steps * StepFee;
    }
}
