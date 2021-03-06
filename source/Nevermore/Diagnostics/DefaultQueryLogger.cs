using Nevermore.Advanced;
using Nevermore.Diagnositcs;

namespace Nevermore.Diagnostics
{
    public class DefaultQueryLogger : IQueryLogger
    {
        static readonly ILog Log = LogProvider.For<DefaultQueryLogger>();

        readonly long infoThreshold;

        public DefaultQueryLogger(long infoThreshold = 300)
        {
            this.infoThreshold = infoThreshold;
        }

        public virtual void Insert(long duration, string transactionName, string statement)
            => Write(duration, $"Insert took {duration}ms in transaction '{transactionName}': {statement}");

        public virtual void Update(long duration, string transactionName, string statement)
            => Write(duration, $"Update took {duration}ms in transaction '{transactionName}': {statement}");

        public virtual void Delete(long duration, string transactionName, string statement)
            => Write(duration, $"Delete took {duration}ms in transaction '{transactionName}': {statement}");

        public virtual void NonQuery(long duration, string transactionName, string statement)
            => Write(duration, $"Execute non query took {duration}ms in transaction '{transactionName}': {statement}");

        public virtual void ProcessReader(long duration, string transactionName, string statement)
            => Write(duration, $"Process reader took {duration}ms in transaction '{transactionName}': {statement}");

        public virtual void ExecuteReader(long duration, string transactionName, string statement)
            => Write(duration, $"Execute reader took {duration}ms in transaction '{transactionName}': {statement}");

        public virtual void Scalar(long duration, string transactionName, string statement)
            => Write(duration, $"Execute scalar took {duration}ms in transaction '{transactionName}': {statement}");

        void Write(long duration, string message)
        {
            var level = duration >= infoThreshold ? LogLevel.Info : LogLevel.Debug;
            Log.Log(level, () => message);
        }
    }
}
