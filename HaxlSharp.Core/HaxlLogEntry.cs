using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface HaxlLogEntry
    {
        X Match<X>(Func<InformationLogEntry, X> info, Func<WarningLogEntry, X> warn, Func<ErrorLogEntry, X> error);
        string ToDefaultString();
   }

    public abstract class BaseLogEntry : HaxlLogEntry
    {
        public readonly DateTime Timestamp;
        public abstract string Message { get; }
        public abstract string Type { get; }

        public BaseLogEntry()
        {
            Timestamp = DateTime.Now;
        }

        public abstract X Match<X>(Func<InformationLogEntry, X> info, Func<WarningLogEntry, X> warn, Func<ErrorLogEntry, X> error);

        public string ToDefaultString()
        {
            return $"[{Timestamp.ToShortDateString()}] {Type.PadLeft(5)}: {Message}";
        }
    }

    public class InformationLogEntry : BaseLogEntry
    {
        public override string Message { get; }
        public override string Type => "INFO";

        public InformationLogEntry(string info)
        {
            Message = info;
        }

        public override X Match<X>(Func<InformationLogEntry, X> info, Func<WarningLogEntry, X> warn, Func<ErrorLogEntry, X> error)
        {
            return info(this);
        }
    }

    public class WarningLogEntry : BaseLogEntry
    {
        public override string Message { get; }
        public override string Type => "WARN";
        public WarningLogEntry(string warning)
        {
            Message = warning;
        }

        public override X Match<X>(Func<InformationLogEntry, X> info, Func<WarningLogEntry, X> warn, Func<ErrorLogEntry, X> error)
        {
            return warn(this);
        }
    }

    public class ErrorLogEntry : BaseLogEntry
    {
        public override string Message { get; }
        public override string Type => "ERROR";
        public ErrorLogEntry(string error)
        {
            Message = error;
        }

        public override X Match<X>(Func<InformationLogEntry, X> info, Func<WarningLogEntry, X> warn, Func<ErrorLogEntry, X> error)
        {
            return error(this);
        }

    }
}
