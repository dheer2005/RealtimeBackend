using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Instagram.Services
{
    public class DbCallInterceptor : DbCommandInterceptor
    {
        public static int DbCallCount = 0;

        public static void Reset() => DbCallCount = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref DbCallCount);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref DbCallCount);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
