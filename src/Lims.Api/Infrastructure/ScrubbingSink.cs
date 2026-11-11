using Microsoft.Data.SqlClient;
using Serilog.Core;
using Serilog.Events;

namespace Lims.Api.Infrastructure;

/// <summary>
/// Stops data values leaking into log files through exception messages. SQL Server error messages quote the values
/// involved ("The duplicate key value is (35202-...)", "Truncated value: 'Muhammad A'"), so for any exception chain that
/// contains a <see cref="SqlException"/> the message text is replaced by the error number, class and state, and the
/// stack trace is kept. Other exceptions are written unchanged.
/// </summary>
public sealed class ScrubbingSink(ILogEventSink inner) : ILogEventSink, IDisposable
{
    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Exception is null || !ContainsSql(logEvent.Exception)) { inner.Emit(logEvent); return; }
        inner.Emit(new LogEvent(logEvent.Timestamp, logEvent.Level, new ScrubbedException(logEvent.Exception),
            logEvent.MessageTemplate, logEvent.Properties.Select(p => new LogEventProperty(p.Key, p.Value))));
    }

    public void Dispose() => (inner as IDisposable)?.Dispose();

    internal static bool ContainsSql(Exception? e)
    {
        for (var depth = 0; e is not null && depth < 20; e = e.InnerException, depth++)
        {
            if (e is SqlException) return true;
            if (e is AggregateException agg && agg.InnerExceptions.Any(ContainsSql)) return true;
        }
        return false;
    }

    internal static string Describe(Exception? e, int depth = 0)
    {
        if (e is null || depth > 20) return "";
        if (e is SqlException s)
            return $"{e.GetType().FullName}: SQL Server error {s.Number}, class {s.Class}, state {s.State} (message withheld: it may contain data values)";
        // Wrappers such as AggregateException copy their inner messages into their own, so a wrapper above a SQL error
        // is described by its type only.
        if (!ContainsSql(e)) return $"{e.GetType().FullName}: {e.Message}";
        var children = e is AggregateException agg ? agg.InnerExceptions.AsEnumerable() : e.InnerException is null ? [] : [e.InnerException];
        return $"{e.GetType().FullName}: (message withheld) ---> " + string.Join(" | ", children.Select(c => Describe(c, depth + 1)));
    }
}

/// <summary>The same exception without the SQL Server message text.</summary>
public sealed class ScrubbedException(Exception original) : Exception(ScrubbingSink.Describe(original))
{
    public override string? StackTrace => original.StackTrace;

    /// <summary>The scrubbed description followed by the stack trace of every exception in the chain.</summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder(Message);
        var stacks = new List<Exception>();
        void Walk(Exception? e, int depth)
        {
            if (e is null || depth > 20) return;
            stacks.Add(e);
            if (e is AggregateException agg) foreach (var i in agg.InnerExceptions) Walk(i, depth + 1);
            else Walk(e.InnerException, depth + 1);
        }
        Walk(original, 0);
        foreach (var e in stacks.Where(x => x.StackTrace is not null))
            sb.AppendLine().Append("   --- stack of ").Append(e.GetType().Name).AppendLine(" ---").Append(e.StackTrace);
        return sb.ToString();
    }
}
