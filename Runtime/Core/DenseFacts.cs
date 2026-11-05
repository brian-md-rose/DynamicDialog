using System;

namespace DynamicDialog
{
    /// <summary>
    /// A query's facts indexed directly by symbol ID, so each criterion is one array read instead of
    /// a walk through the sorted query. The matcher loads a query once per lookup, then tests many
    /// rules against it. A stamp marks which slots belong to the current query, so loading never
    /// clears the arrays. Allocates only when the symbol table grows.
    /// </summary>
    internal sealed class DenseFacts
    {
        float[] values = new float[256];
        int[] stamps = new int[256];
        int stamp;

        public void Load(Query query)
        {
            if (++stamp == int.MaxValue)
            {
                Array.Clear(stamps, 0, stamps.Length);
                stamp = 1;
            }

            int maxId = 0;
            for (int i = 0; i < query.Count; i++) maxId = Math.Max(maxId, query.KeyAt(i).Id);
            if (maxId >= stamps.Length)
            {
                int size = Math.Max(maxId + 1, Math.Max(stamps.Length * 2, SymbolTable.Count + 1));
                Array.Resize(ref values, size);
                Array.Resize(ref stamps, size);
            }

            for (int i = 0; i < query.Count; i++)
            {
                int id = query.KeyAt(i).Id;
                values[id] = query.ValueAt(i);
                stamps[id] = stamp;
            }
        }

        /// <summary>Same result as <see cref="Rule.FirstFailedCriterion(Query)"/> for the loaded query.</summary>
        public int FirstFailedCriterion(Rule rule)
        {
            var criteria = rule.Criteria;
            int n = stamps.Length;
            for (int ci = 0; ci < criteria.Length; ci++)
            {
                ref readonly Criterion c = ref criteria[ci];
                int id = c.Key.Id;
                bool present = id < n && stamps[id] == stamp;
                if (!c.Test(present, present ? values[id] : 0f)) return ci;
            }
            return -1;
        }
    }
}
