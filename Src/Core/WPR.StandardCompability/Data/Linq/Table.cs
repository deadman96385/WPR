using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace System.Data.Linq
{
    /* A table read out of the phone's local database, presented as the
     * IQueryable<T> the phone's LINQ to SQL returned.
     *
     * There is no query translator here and none is needed. The rows are read
     * once and held, and the queries a title builds - including the expression
     * trees Trivial Pursuit assembles by hand out of Expression.Parameter,
     * Property and Call - are evaluated against them by the enumerable query
     * provider the framework already ships. A translator would only be worth
     * writing if a database were large enough that reading it whole was the
     * problem, and these ship inside the package.
     */
    public sealed class Table<TEntity> : IQueryable<TEntity>, IEnumerable<TEntity>, IQueryable
        where TEntity : class
    {
        private readonly IQueryable<TEntity> rows;

        internal Table(IEnumerable<TEntity> entities)
        {
            rows = entities.ToList().AsQueryable();
        }

        public Type ElementType
        {
            get { return rows.ElementType; }
        }

        public Expression Expression
        {
            get { return rows.Expression; }
        }

        public IQueryProvider Provider
        {
            get { return rows.Provider; }
        }

        public IEnumerator<TEntity> GetEnumerator()
        {
            return rows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
