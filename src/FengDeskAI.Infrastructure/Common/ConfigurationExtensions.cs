using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FengDeskAI.Infrastructure.Common;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Bind config section vào options class. Class T phải có
    /// <c>public const string SectionName = "...";</c>
    /// </summary>
    /// <example>
    /// <code>services.AddSettings&lt;MailSettings&gt;(configuration);</code>
    /// </example>
    public static IServiceCollection AddSettings<T>(this IServiceCollection services, IConfiguration configuration)
        where T : class
    {
        var sectionName = GetSectionName<T>();
        services.Configure<T>(configuration.GetSection(sectionName));
        return services;
    }

    /// <summary>
    /// Đọc và trả về POCO của options (không qua DI). Dùng cho code cần
    /// giá trị config ngay tại thời điểm bootstrap (ví dụ JWT validation params).
    /// </summary>
    public static T GetSettings<T>(this IConfiguration configuration) where T : class, new()
    {
        var sectionName = GetSectionName<T>();
        var settings = new T();
        configuration.GetSection(sectionName).Bind(settings);
        return settings;
    }

    private static string GetSectionName<T>()
        => typeof(T).GetField("SectionName", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
               ?.GetValue(null) as string
           ?? throw new InvalidOperationException(
               $"{typeof(T).Name} phải định nghĩa: public const string SectionName = \"...\";");
}
