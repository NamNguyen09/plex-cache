using EFCoreCache.Tables;
using Microsoft.EntityFrameworkCore;

namespace EFCoreCache.Interfaces;
public interface IEFCoreSqlCommandsProcessor
{
    /// <summary>
    ///     Extracts the table names of an SQL command.
    /// </summary>
    SortedSet<string> GetSqlCommandTableNames(string commandText);

    /// <summary>
    ///     Extracts the entity types of an SQL command.
    /// </summary>
    IList<Type> GetSqlCommandEntityTypes(string commandText, IList<TableEntityInfo> allEntityTypes);

    /// <summary>
    ///     Returns all of the given context's table names.
    /// </summary>
    IList<TableEntityInfo> GetAllTableNames(DbContext context);

    /// <summary>
    ///     Is `insert`, `update` or `delete`?
    /// </summary>
    bool IsCrudCommand(string text);
}
