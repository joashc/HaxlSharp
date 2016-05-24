using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface Hfi<C, X>
    {
        X Bind<B>(HF<B> hf, Expression<Func<B, HF<C>>> bind);
        X Applicative<A, B>(HF<A> hf, Func<HF<B>> applicative, Func<A, B, C> project);
        X Identity(C value);
    }

    public interface HF<A>
    {
        X Run<X>(Hfi<A, X> interpreter);
    }

    public class BindH<B, C> : HF<C>
    {
        public readonly HF<B> hf;
        public readonly Expression<Func<B, HF<C>>> bind;
        public BindH(HF<B> hf, Expression<Func<B, HF<C>>> bind)
        {
            this.hf = hf;
            this.bind = bind;
        }

        public X Run<X>(Hfi<C, X> interpreter)
        {
            return interpreter.Bind(hf, bind);
        }
    }

    public class ApplicativeH<A, B, C> : HF<C>
    {
        public readonly HF<A> hfA;
        public readonly Func<HF<B>> hfB;
        public readonly Func<A, B, C> project;
        public ApplicativeH(HF<A> hfA, Func<HF<B>> hfB, Func<A, B, C> project)
        {
            this.hfA = hfA;
            this.hfB = hfB;
            this.project = project;
        }

        public X Run<X>(Hfi<C, X> interpreter)
        {
            return interpreter.Applicative(hfA, hfB, project);
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

    public class Query<C> : Hfi<C, C>
    {
        public C Applicative<A, B>(HF<A> hf, Func<HF<B>> applicative, Func<A, B, C> project)
        {
            Debug.WriteLine("BATCH");
            var resultA = Task.Factory.StartNew(() => hf.Run(new Query<A>()));
            var resultB = Task.Factory.StartNew(() => applicative().Run(new Query<B>()));
            Task.WaitAll(resultA, resultB);
            return project(resultA.Result, resultB.Result);
        }

        public C Bind<B>(HF<B> hf, Expression<Func<B, HF<C>>> bind)
        {
            var resultB = hf.Run(new Query<B>());
            var compiledBind = bind.Compile();
            var hfC = compiledBind(resultB);
            var resultC = hfC.Run(this);
            return resultC;
        }

        public C Identity(C value)
        {
            return value;
        }
    }

    public static class HFExt
    {
        public static HF<B> Select<A, B>(this HF<A> self, Expression<Func<A, B>> f)
        {
            var compiled = f.Compile();
            return new BindH<A, B>(self, a => new Identity<B>(compiled(a)));
        }

        public static HF<B> SelectMany<A, B>(this HF<A> self, Expression<Func<A, HF<B>>> bind)
        {
            return new BindH<A, B>(self, bind);
        }

        public static HF<C> SelectMany<A, B, C>(this HF<A> self,
            Expression<Func<A, HF<B>>> bind, Expression<Func<A, B, C>> project)
        {
            var compiledBind = bind.Compile();
            var compiledProject = project.Compile();

            if (DetectApplicative.IsApplicative(bind)) return new ApplicativeH<A, B, C>(self, () => compiledBind(default(A)), compiledProject);

            return new BindH<A, C>(self, a => new BindH<B, C>(compiledBind(a),
                b => new Identity<C>(compiledProject(a, b))));
        }


    }
}
