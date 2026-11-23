namespace EduPlatform.Application.Common.Caching;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CachedAttribute : Attribute
{
    public CachedAttribute(int ttlSeconds = 60) => TtlSeconds = ttlSeconds;
    public int TtlSeconds { get; }
}
