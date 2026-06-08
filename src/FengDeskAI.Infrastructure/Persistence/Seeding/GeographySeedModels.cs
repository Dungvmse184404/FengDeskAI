namespace FengDeskAI.Infrastructure.Persistence.Seeding;

// Khớp cấu trúc JSON của provinces.open-api.vn (?depth=3). Các field thừa được bỏ qua.
internal sealed record ProvinceSeed(string Name, int Code, List<DistrictSeed>? Districts);
internal sealed record DistrictSeed(string Name, int Code, List<WardSeed>? Wards);
internal sealed record WardSeed(string Name, int Code);
