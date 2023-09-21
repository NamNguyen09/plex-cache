using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;

namespace EFCoreCache.RedisCaches;
public class BinarySerializer
{
    public byte[] Serialize<T>(T o) where T : class
    {
        if (o == null) return null;
        var binaryFormatter = new BinaryFormatter();

        using (var memoryStream = new MemoryStream())
        {
#pragma warning disable SYSLIB0011 // Type or member is obsolete
            binaryFormatter.Serialize(memoryStream, o);
#pragma warning restore SYSLIB0011 // Type or member is obsolete
            var objectDataAsStream = memoryStream.ToArray();
            return objectDataAsStream;
        }
    }

    public T Deserialize<T>(byte[] stream)
    {
        if (stream == null || !stream.Any()) return default(T);
        var binaryFormatter = new BinaryFormatter();
        using (var memoryStream = new MemoryStream(stream))
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
#pragma warning disable SYSLIB0011 // Type or member is obsolete
            var result = (T)binaryFormatter.Deserialize(memoryStream);
#pragma warning restore SYSLIB0011 // Type or member is obsolete
            return result;
        }
    }
    public bool TryDeserialize<T>(byte[] stream, out T result)
    {
        try
        {
            if (stream == null || !stream.Any())
            {
                result = default(T);
                return true;
            }
            var binaryFormatter = new BinaryFormatter();
            binaryFormatter.Binder = new IgnoreMissingAsemblySerializationBinder();
            using (var memoryStream = new MemoryStream(stream))
            {
                memoryStream.Seek(0, SeekOrigin.Begin);
#pragma warning disable SYSLIB0011 // Type or member is obsolete
                result = (T)binaryFormatter.Deserialize(memoryStream);
#pragma warning restore SYSLIB0011 // Type or member is obsolete
            }
            return true;

        }
        catch (Exception ex)
        {
            result = default(T);
            return false;
        }
    }
}
public sealed class IgnoreMissingAsemblySerializationBinder : System.Runtime.Serialization.SerializationBinder
{
    public override Type BindToType(string assemblyName, string typeName)
    {
        Type type = null;
        string sShortAssemblyName = assemblyName.Split(',')[0];

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (Assembly assembly in assemblies)
        {
            if (assembly == null || string.IsNullOrWhiteSpace(assembly.FullName)) continue;
            if (sShortAssemblyName == assembly.FullName.Split(',')[0])
            {
                type = assembly.GetType(typeName);
                break;
            }
        }
        return type ?? typeof(Object);
    }
}
