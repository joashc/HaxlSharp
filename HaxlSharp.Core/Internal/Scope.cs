using System;
using System.Collections.Generic;
using System.Linq;
using static HaxlSharp.Internal.Base;

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

        public bool IsRoot => parentScope == null;

        public virtual object GetValue(string variableName)
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
            return this;
        }

        public int GetLatestBlockNumber()
        {
            if (!Keys.Any()) return 0;
            return Keys.Select(GetBlockNumber).Max();
        }

        public Scope WriteParent(string name, object value)
        {
            if (parentScope == null) return Add(name, value);
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

        public IEnumerable<object> ShallowValues => boundVariables.Values;
    }

    public class SelectScope : Scope
    {
        private readonly object _selectValue;
        public SelectScope(object selectValue, Scope scope) : base(scope)
        {
            _selectValue = selectValue;
        }

        public override object GetValue(string variableName)
        {
            try
            {
                return base.GetValue(variableName);
            }
            catch (ArgumentException)
            {
                return _selectValue;
            }
            
        }
    }
}
