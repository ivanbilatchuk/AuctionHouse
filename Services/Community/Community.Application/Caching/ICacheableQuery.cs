namespace Community.Application.Caching;

// Запит MediatR, відповідь якого можна кешувати (CachingBehavior). null — цей конкретний запит не кешується
// (напр. довільні фільтри/пошук дають надто багато комбінацій ключів).
public interface ICacheableQuery
{
    CacheDescriptor? GetCacheDescriptor();
}

// Key — ключ запису; VersionKey — ключ "версії списку": інвалідація видаляє версію, і всі сторінки списку
// автоматично стають недосяжними (старі записи доживають свій TTL у Redis).
public sealed record CacheDescriptor(string Key, string? VersionKey, CacheEntrySettings Settings);
