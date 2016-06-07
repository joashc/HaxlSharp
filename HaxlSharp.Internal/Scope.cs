using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// Scope with simple inheritance that "shadows" variable names.
    /// </summary>
    public class Scope
    {
        private readonly Dictionary<string, object> boundVariables;
        private readonly Scope parentScope;

        public static Scope New()
        {
            return new Scope();
        }

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

        public bool IsRoot { get { return parentScope == null; } }

        private Scope(Dictionary<string, object> dic, Scope scope)
        {
            boundVariables = dic;
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

        public Scope Add(string name, object value)
        {
            boundVariables[name] = value;
            var newDic = new Dictionary<string, object>(boundVariables, null);
            return new Scope(newDic, parentScope);
        }

        public Scope WriteParent(string name, object value)
        {
            return parentScope.Add(name, value);
        }

        public IEnumerable<string> Keys
        {
            get
            {
                if (parentScope == null) return boundVariables.Keys;
                return boundVariables.Keys.Concat(parentScope.Keys);
            }
        }

        public IEnumerable<object> ShallowValues
        {
            get
            {
                return boundVariables.Values;
            }
        }
    }
}
