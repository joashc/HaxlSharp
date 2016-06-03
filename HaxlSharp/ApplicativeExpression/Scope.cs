using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class Scope
    {
        private readonly Dictionary<string, object> boundVariables;
        private readonly Scope parentScope;

        public Scope()
        {
            boundVariables = new Dictionary<string, object>();
            parentScope = null;
        }

        public Scope(Scope scope)
        {
            boundVariables = new Dictionary<string, object>();
            parentScope = scope;
        }

        public object GetValue(string variableName)
        {
            if (boundVariables.ContainsKey(variableName)) return boundVariables[variableName];
            if (parentScope == null) throw new ArgumentException($"No variable named '{variableName}' in scope.");
            return parentScope.GetValue(variableName);
        }

        public bool InScope(string variableName)
        {
            if (boundVariables.ContainsKey(variableName)) return true;
            if (parentScope == null) return false;
            return parentScope.InScope(variableName);
        }

        public void Add(string name, object value)
        {
            boundVariables[name] = value;
        }

        public IEnumerable<string> Keys
        {
            get
            {
                if (parentScope == null) return boundVariables.Keys;
                return boundVariables.Keys.Concat(parentScope.Keys);
            }
        }
    }
}
