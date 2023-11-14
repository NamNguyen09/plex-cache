using System.Collections.Concurrent;
using System.Reflection;
using cx.BinarySerializer.EFCache.Tables;
using EFCoreCache.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EFCoreCache.Processors;
public class EFCoreSqlCommandsProcessor : IEFCoreSqlCommandsProcessor
{
    private static readonly string[] CrudMarkers = { "MERGE ", "insert ", "update ", "delete ", "create " };

    private static readonly Type IEntityType =
        Type.GetType("Microsoft.EntityFrameworkCore.Metadata.IEntityType, Microsoft.EntityFrameworkCore") ??
        throw new TypeLoadException("Couldn't load Microsoft.EntityFrameworkCore.Metadata.IEntityType");

    private static readonly PropertyInfo ClrTypePropertyInfo =
        IEntityType.GetInterfaces()
                   .Union(new[] { IEntityType })
                   .Select(i => i.GetProperty("ClrType", BindingFlags.Public | BindingFlags.Instance))
                   .Distinct()
                   .FirstOrDefault(propertyInfo => propertyInfo != null) ??
        throw new KeyNotFoundException("Couldn't find `ClrType` on IEntityType.");

    private static readonly Type RelationalEntityTypeExtensionsType =
        Type.GetType(
                     "Microsoft.EntityFrameworkCore.RelationalEntityTypeExtensions, Microsoft.EntityFrameworkCore.Relational") ??
        throw new TypeLoadException("Couldn't load Microsoft.EntityFrameworkCore.RelationalEntityTypeExtensions");

    private static readonly MethodInfo GetTableNameMethodInfo =
        RelationalEntityTypeExtensionsType.GetMethod("GetTableName", BindingFlags.Static | BindingFlags.Public)
        ?? throw new KeyNotFoundException("Couldn't find `GetTableName()` on RelationalEntityTypeExtensions.");

    private readonly ConcurrentDictionary<string, Lazy<SortedSet<string>>> _commandTableNames =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<Type, Lazy<List<TableEntityInfo>>> _contextTableNames = new();
    private readonly IEFCoreHashProvider _hashProvider;

    /// <summary>
    ///     SqlCommands Utils
    /// </summary>
    public EFCoreSqlCommandsProcessor(IEFCoreHashProvider hashProvider) =>
        _hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));

    /// <summary>
    ///     Is `insert`, `update` or `delete`?
    /// </summary>
    public bool IsCrudCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            foreach (var marker in CrudMarkers)
            {
                if (!line.Trim().StartsWith(marker, StringComparison.OrdinalIgnoreCase)) continue;

                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns all of the given context's table names.
    /// </summary>
    public IList<TableEntityInfo> GetAllTableNames(DbContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return _contextTableNames.GetOrAdd(context.GetType(),
                                           _ => new Lazy<List<TableEntityInfo>>(() => GetTableNames(context),
                                                                                LazyThreadSafetyMode
                                                                                    .ExecutionAndPublication)).Value;
    }

    /// <summary>
    ///     Extracts the table names of an SQL command.
    /// </summary>
    public SortedSet<string> GetSqlCommandTableNames(string commandText)
    {
        var commandTextKey = $"{_hashProvider.ComputeHash(commandText):X}";
        return _commandTableNames.GetOrAdd(commandTextKey,
                                           _ => new Lazy<SortedSet<string>>(() => GetRawSqlCommandTableNames(commandText),
                                                        LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    /// <summary>
    ///     Extracts the entity types of an SQL command.
    /// </summary>
    public IList<Type> GetSqlCommandEntityTypes(string commandText, IList<TableEntityInfo> allEntityTypes)
    {
        var commandTableNames = GetSqlCommandTableNames(commandText);
        return allEntityTypes.Where(entityType => commandTableNames.Contains(entityType.TableName))
                             .Select(entityType => entityType.ClrType)
                             .ToList();
    }

    private static List<TableEntityInfo> GetTableNames(DbContext context)
    {
        var tableNames = new List<TableEntityInfo>();
        var entityTypes = context.Model.GetEntityTypes();
        foreach (var entityType in entityTypes)
        {
            var clrType = GetClrType(entityType);
            if (clrType == null) continue;
            tableNames.Add(
                           new TableEntityInfo
                           {
                               ClrType = clrType,
                               TableName = GetTableName(entityType) ?? clrType.ToString()
                           });
        }

        return tableNames;
    }

    private static string? GetTableName(object entityType)
    {
        return GetTableNameMethodInfo.Invoke(null, new[] { entityType }) as string;
    }

    private static Type GetClrType(object entityType)
    {
        var value = ClrTypePropertyInfo.GetValue(entityType) ??
                    throw new InvalidOperationException($"Couldn't get the ClrType value of `{entityType}`");
        return (Type)value;
    }

    private static SortedSet<string> GetRawSqlCommandTableNames(string commandText)
    {
        string[] tableMarkers = { "FROM", "JOIN", "INTO", "UPDATE", "MERGE" };

        var tables = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        var sqlItems = commandText.Split(new[] { " ", "\r\n", Environment.NewLine, "\n" },
                                         StringSplitOptions.RemoveEmptyEntries);
        var sqlItemsLength = sqlItems.Length;
        for (var i = 0; i < sqlItemsLength; i++)
        {
            foreach (var marker in tableMarkers)
            {
                if (!sqlItems[i].Equals(marker, StringComparison.OrdinalIgnoreCase)) continue;

                ++i;

                if (i >= sqlItemsLength) continue;

                var tableName = string.Empty;

                var tableNameParts = sqlItems[i].Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                if (tableNameParts.Length == 1)
                {
                    tableName = tableNameParts[0].Trim();
                }
                else if (tableNameParts.Length >= 2)
                {
                    tableName = tableNameParts[1].Trim();
                }

                if (string.IsNullOrWhiteSpace(tableName)) continue;

                tableName = tableName.Replace("[", "", StringComparison.Ordinal)
                                     .Replace("]", "", StringComparison.Ordinal)
                                     .Replace("'", "", StringComparison.Ordinal)
                                     .Replace("`", "", StringComparison.Ordinal)
                                     .Replace("\"", "", StringComparison.Ordinal);

                if (tableName == "(") continue;
                tables.Add(tableName);
            }
        }

        return tables;
    }
}