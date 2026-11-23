namespace EduPlatform.Application.Common.Caching;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class InvalidatesCacheAttribute : Attribute
{
    public InvalidatesCacheAttribute(string key) => Key = key;
    public string Key { get; }
}
