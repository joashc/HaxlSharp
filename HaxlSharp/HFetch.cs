using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface Hfi<A, X>
    {
        X Bind<B>(HF<B> hf, Expression<Func<B, HF<A>>> bind);
        X Identity(A value);
    }

    public interface HF<A>
    {
        X Run<X>(Hfi<A, X> interpreter);
    }

    public class Bind<B, A> : HF<A>
    {
        public readonly HF<B> hf;
        public readonly Expression<Func<B, HF<A>>> bind;
        public readonly IEnumerable<string> boundVars;
        public Bind(HF<B> hf, Expression<Func<B, HF<A>>> bind, IEnumerable<string> boundVars)
        {
            this.hf = hf;
            this.bind = bind;
            this.boundVars = boundVars;
        }


        public X Run<X>(Hfi<A, X> interpreter)
        {
            return interpreter.Bind(hf, bind);
        }
    }

    public class Identity<A> : HF<A>
    {
        public readonly A Value;
        public Identity(A value)
        {
            Value = value;
        }

        public X Run<X>(Hfi<A, X> interpreter)
        {
            return interpreter.Identity(Value);
        }
    }

    public class Query<A> : Hfi<A, A>
    {
        public A Bind<B>(HF<B> hf, Expression<Func<B, HF<A>>> bind)
        {
            var resultB = hf.Run(new Query<B>());
            var compiledBind = bind.Compile();
            var hfa = compiledBind(resultB);
            var resultA = hfa.Run(this);
            return resultA;
        }

        public A Identity(A value)
        {
            return value;
        }
    }

    public static class HFExt
    {
        public static HF<B> Select<A, B>(this HF<A> self, Expression<Func<A, B>> f)
        {
            var compiled = f.Compile();
            var bb = new List<string>();
            return new Bind<A, B>(self, a => new Identity<B>(compiled(a)), bb);
        }

        public static HF<B> SelectMany<A, B>(this HF<A> self, Expression<Func<A, HF<B>>> bind)
        {
            var bb = new List<string>();
            return new Bind<A, B>(self, bind, bb);
        }

        public static HF<C> SelectMany<A, B, C>(this HF<A> self,
            Expression<Func<A, HF<B>>> bind, Expression<Func<A, B, C>> project)
        {
            var visitor = new ExpressionArguments();
            visitor.Visit(bind);

            var freeVariables = visitor.parameters.SelectMany(MemberNames).ToList();
            var boundVariables = visitor.arguments.Select(MemberName);
            var compiledBind = bind.Compile();
            var compiledProject = project.Compile();
            var isApplicative = boundVariables.All(bound => !freeVariables.Contains(bound)) && freeVariables.Distinct().Count() == freeVariables.Count();

            var bb = new List<string>();
            return new Bind<A, C>(self, a => new Bind<B, C>(compiledBind(a),
                b => new Identity<C>(compiledProject(a, b)), bb), bb);
        }


        private static IEnumerable<string> MemberNames(ParameterExpression parameter)
        {
            if (parameter.Name.StartsWith("<>"))
            {
                var members = parameter.Type.GetMembers();
                return members.Where(m => m.MemberType == System.Reflection.MemberTypes.Property).Select(m => m.Name);
            }
            return new List<string> { parameter.Name };
        }

        private static string MemberName(MemberExpression memberAccess)
        {
            return memberAccess.Member.Name;
        }
    }
}
