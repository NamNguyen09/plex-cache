using System.Runtime.Serialization;

namespace EFCoreCache.RedisCaches;

[Serializable]
[DataContract]
public class CacheEntry
{
    private readonly object _value;
    private readonly string[] _entitySets;

    public CacheEntry(object value, string[] entitySets)
    {
        _value = value;
        _entitySets = entitySets;
    }

    [DataMember]
    public object Value
    {
        get { return _value; }
    }

    [DataMember]
    public string[] EntitySets
    {
        get { return _entitySets; }
    }
}
