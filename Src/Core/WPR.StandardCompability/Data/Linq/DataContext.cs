using System;
using System.Collections.Generic;
using System.Data.Linq.Mapping;
using System.IO;
using System.Reflection;

namespace System.Data.Linq
{
    /* The phone's LINQ to SQL entry point, over a local database file that
     * ships inside the package.
     *
     * A title's generated context declares its tables and never assigns them -
     * Trivial Pursuit's QuestionsDatabase has a public Table<DatabaseQuestion>
     * field, and its constructor does nothing but call this one - so filling
     * them in is this constructor's job, as it was the phone's.
     */
    public class DataContext : IDisposable
    {
        private readonly Dictionary<Type, object> tables = new Dictionary<Type, object>();

        public DataContext(string fileOrServerOrConnection)
        {
            Connection = fileOrServerOrConnection;
            DatabasePath = ResolveDatabasePath(fileOrServerOrConnection);
            LoadDeclaredTables();
        }

        public string Connection { get; private set; }

        protected string DatabasePath { get; private set; }

        public Table<TEntity> GetTable<TEntity>() where TEntity : class
        {
            return (Table<TEntity>)GetTable(typeof(TEntity));
        }

        public object GetTable(Type entityType)
        {
            if (!tables.TryGetValue(entityType, out object? table))
            {
                table = BuildTable(entityType);
                tables[entityType] = table;
            }

            return table;
        }

        public void Dispose()
        {
            tables.Clear();
            GC.SuppressFinalize(this);
        }

        /* "Data Source = 'appdata:/database/en_us.sdf'; File Mode = read only;"
         * The appdata scheme is the package root, which is the working directory
         * a title runs in.
         */
        private static string ResolveDatabasePath(string connection)
        {
            string value = connection ?? string.Empty;

            int source = value.IndexOf("Data Source", StringComparison.OrdinalIgnoreCase);
            if (source >= 0)
            {
                int separator = value.IndexOf('=', source);
                if (separator >= 0)
                {
                    value = value.Substring(separator + 1);
                }
            }

            int terminator = value.IndexOf(';');
            if (terminator >= 0)
            {
                value = value.Substring(0, terminator);
            }

            value = value.Trim().Trim('\'', '"').Trim();

            foreach (string scheme in new[] { "appdata:/", "appdata:", "isostore:/", "isostore:" })
            {
                if (value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                {
                    value = value.Substring(scheme.Length);
                    break;
                }
            }

            value = value.TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            return Path.IsPathRooted(value)
                ? value
                : Path.Combine(Directory.GetCurrentDirectory(), value);
        }

        private void LoadDeclaredTables()
        {
            foreach (FieldInfo field in GetType().GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Type? entity = EntityTypeOf(field.FieldType);
                if (entity != null)
                {
                    field.SetValue(this, GetTable(entity));
                }
            }

            foreach (PropertyInfo property in GetType().GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Type? entity = EntityTypeOf(property.PropertyType);
                if (entity != null && property.CanWrite)
                {
                    property.SetValue(this, GetTable(entity));
                }
            }
        }

        private static Type? EntityTypeOf(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Table<>))
            {
                return type.GetGenericArguments()[0];
            }

            return null;
        }

        private object BuildTable(Type entityType)
        {
            PropertyInfo[] columns = MappedColumns(entityType);
            var entities = (System.Collections.IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(entityType))!;

            if (columns.Length > 0 && File.Exists(DatabasePath))
            {
                var columnTypes = new Type[columns.Length];
                for (int index = 0; index < columns.Length; index++)
                {
                    columnTypes[index] = columns[index].PropertyType;
                }

                foreach (object?[] row in LocalDatabaseReader.ReadRows(DatabasePath, columnTypes))
                {
                    object entity = Activator.CreateInstance(entityType)!;
                    for (int index = 0; index < columns.Length; index++)
                    {
                        if (row[index] != null && columns[index].CanWrite)
                        {
                            columns[index].SetValue(entity, row[index]);
                        }
                    }

                    entities.Add(entity);
                }
            }

            return Activator.CreateInstance(
                typeof(Table<>).MakeGenericType(entityType),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { entities },
                null)!;
        }

        /* Declaration order is the column order on disk, and reflection does not
         * promise it, so the columns are ordered by metadata token instead.
         */
        private static PropertyInfo[] MappedColumns(Type entityType)
        {
            var mapped = new List<PropertyInfo>();
            foreach (PropertyInfo property in entityType.GetProperties(
                BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.IsDefined(typeof(ColumnAttribute), true))
                {
                    mapped.Add(property);
                }
            }

            mapped.Sort((left, right) => left.MetadataToken.CompareTo(right.MetadataToken));
            return mapped.ToArray();
        }
    }
}
