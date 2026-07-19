using System;

/* The phone's LINQ to SQL mapping attributes. Only the shapes a title's
 * generated data classes carry are needed: the attributes are read by our own
 * DataContext to work out which properties are columns, and the named arguments
 * a designer emits must all exist or the attribute fails to load.
 */
namespace System.Data.Linq.Mapping
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class TableAttribute : Attribute
    {
        public string? Name { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ColumnAttribute : Attribute
    {
        public ColumnAttribute()
        {
            CanBeNull = true;
            UpdateCheck = UpdateCheck.Always;
            AutoSync = AutoSync.Default;
        }

        public string? Name { get; set; }
        public string? Storage { get; set; }
        public string? DbType { get; set; }
        public string? Expression { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsDbGenerated { get; set; }
        public bool IsDiscriminator { get; set; }
        public bool IsVersion { get; set; }
        public bool CanBeNull { get; set; }
        public UpdateCheck UpdateCheck { get; set; }
        public AutoSync AutoSync { get; set; }
    }

    public enum UpdateCheck
    {
        Always,
        Never,
        WhenChanged
    }

    public enum AutoSync
    {
        Default,
        Always,
        Never,
        OnInsert,
        OnUpdate
    }
}
