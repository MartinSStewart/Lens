using Lens;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LensTests
{
    /// <summary>
    /// </summary>
    /// <remarks>Original code found here: https://stackoverflow.com/a/16336563 </remarks>
    [TestFixture]
    class LensTest
    {
        class A : IRecord
        {
            public int P { get; private set; }
            public B B { get; private set; }
            public A(int p = 0, B b = null)
            {
                P = p;
                B = b;
            }
        }

        class B : IRecord
        {
            public int P { get; private set; }
            public C C { get; private set; }
            public B(int p = 0, C c = null)
            {
                P = p;
                C = c;
            }
        }

        class C : IRecord
        {
            public int P { get; private set; }
            public C(int p = 0)
            {
                P = p;
            }
        }

        class D : IState
        {
            public string Text { get; private set; }
            public E E { get; private set; } = new E();

            public bool IsValid() => Text != null;
        }

        class E : IState
        {
            public double Value { get; private set; }

            public bool IsValid() => Value >= 0;
        }

        [Test]
        public void SetterSpec()
        {
            var a = new A(
                p: 10, 
                b: new B(
                    p: 20, 
                    c: new C(30)));

            var result = a.Set(p => p.B.C.P, 10);

            Assert.AreEqual(30, a.B.C.P);
            Assert.AreEqual(10, result.B.C.P);
        }

        [Test]
        public void LensSetWithSingleProperty()
        {
            var a = new A();

            var expected = new A(1);
            var result = a.Set(p => p, expected);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void StringListGettersShouldWork()
        {
            var a = new A(
                p: 10, 
                b: new B(
                    p: 20, 
                    c: new C(30)));

            var result = a.Set(p => p.B.C.P, 10);

            Assert.AreEqual(30, a.B.C.P);
            Assert.AreEqual(10, result.B.C.P);
        }

        [Test]
        public void LensWithFuncValue()
        {
            var a = new A(
                p: 10,
                b: new B(
                    p: 20,
                    c: new C(30)));

            var result = a.Set(p => p.B.C.P, val => val + 1);

            Assert.AreEqual(30, a.B.C.P);
            Assert.AreEqual(31, result.B.C.P);
        }

        [Test]
        public void LensWithSinglePropertyChecksIsValidForIState()
        {
            Assert.Throws<DebugExException>(() => new D().Set(p => p, new D()));
        }

        [Test]
        public void LensWithSinglePropertyHandlesNull()
        {
            var result = new D().Set(p => p, _ => null);
            Assert.AreEqual(null, result);
        }

        [Test]
        public void LensChecksIsValidForIState()
        {
            Assert.Throws<DebugExException>(() => new D().Set(p => p.E.Value, -1));
        }

        [Test]
        [Explicit("This is a benchmark, not a unit test.")]
        public static void BenchmarkLens()
        {
            var a = new A(
                p: 10,
                b: new B(
                    p: 20,
                    c: new C(30)));

            var randomActions = new Action[]
            {
                () => a.Set(p => p.P, 3),
                () => a.Set(p => p.B, new B()),
                () => a.Set(p => p.B.C, new C()),
                () => a.Set(p => p.B.C.P, val => val + 1)
            };

            var random = new Random(123123);

            var result = Benchmark.Run(() => randomActions[random.Next(randomActions.Length)].Invoke(), 1000);
            Console.WriteLine(result);
        }
    }
}
