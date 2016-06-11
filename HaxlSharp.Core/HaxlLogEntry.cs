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
   }

    public abstract class BaseLogEntry : HaxlLogEntry
    {
        public readonly DateTime Timestamp;
        public BaseLogEntry()
        {
            Timestamp = DateTime.Now;
        }

        public abstract X Match<X>(Func<InformationLogEntry, X> info, Func<WarningLogEntry, X> warn, Func<ErrorLogEntry, X> error);
    }

    public class InformationLogEntry : BaseLogEntry
    {
        public readonly string Information;
        public InformationLogEntry(string info)
        {
            Information = info;
        }

        public override X Match<X>(Func<InformationLogEntry, X> info, Func<WarningLogEntry, X> warn, Func<ErrorLogEntry, X> error)
        {
            return info(this);
        }
    }

    public class WarningLogEntry : BaseLogEntry
    {
        public readonly string Warning;
        public WarningLogEntry(string warning)
        {
            Warning = warning;
        }

        public override X Match<X>(Func<InformationLogEntry, X> info, Func<WarningLogEntry, X> warn, Func<ErrorLogEntry, X> error)
        {
            return warn(this);
        }
    }

    public class ErrorLogEntry : BaseLogEntry
    {
        public readonly string Error;
        public ErrorLogEntry(string error)
        {
            Error = error;
        }

        public override X Match<X>(Func<InformationLogEntry, X> info, Func<WarningLogEntry, X> warn, Func<ErrorLogEntry, X> error)
        {
            return error(this);
        }
    }
}
