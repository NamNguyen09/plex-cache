namespace EFCoreCache.RedisCaches;

[Serializable]
public class CacheEntry
{
    private readonly object _value;
    private readonly string[] _entitySets;

    public CacheEntry(object value, string[] entitySets)
    {
        _value = value;
        _entitySets = entitySets;
    }

    public object Value
    {
        get { return _value; }
    }

    public string[] EntitySets
    {
        get { return _entitySets; }
    }
}
