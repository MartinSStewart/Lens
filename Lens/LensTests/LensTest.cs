using Lens;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
            public ImmutableList<int> Numbers { get; private set; } = new[] { 1, 2, 3, 4 }.ToImmutableList();
            public ImmutableList<C> Cs { get; private set; } = new[] { new C(), new C(), new C() }.ToImmutableList();
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

            public MultiIndexer MultiIndexer { get; private set; } = new MultiIndexer();

            public bool IsValid() => Value >= 0;
        }

        class MultiIndexer : IState
        {
            private ImmutableList<double> _numberArray = new[] { 10.0 }.ToImmutableList();
            private ImmutableDictionary<string, A> _stringDictionary = ImmutableDictionary<string, A>.Empty;

            public double this[int index]
            {
                get => _numberArray[index];
                private set => _numberArray = _numberArray.SetItem(index, value);
            }

            public A this[string index]
            {
                get => _stringDictionary[index];
                private set => _stringDictionary = _stringDictionary.SetItem(index, value);
            }

            public bool IsValid() => true;
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
            Assert.Throws<InvalidStateException>(() => new D().Set(p => p, new D()));
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
            Assert.Throws<InvalidStateException>(() => new D().Set(p => p.E.Value, -1));
        }

        [Test]
        public void LensCanSetIndex0()
        {
            var a = new MultiIndexer();
            var result = a.Set(p => p[0], 100);
            Assert.AreEqual(100, result[0]);
            Assert.AreEqual(10, a[0]);
        }

        [Test]
        public void LensCanSetIndex1()
        {
            var a = new E();
            var result = a.Set(p => p.MultiIndexer[0], 100);
            Assert.AreEqual(100, result.MultiIndexer[0]);
            Assert.AreEqual(10, a.MultiIndexer[0]);
        }

        [Test]
        public void LensCanSetIndex2()
        {
            var a = new E();
            var result = a.Set(p => p.MultiIndexer[0], v => v + 1);
            Assert.AreEqual(a.MultiIndexer[0] + 1, result.MultiIndexer[0]);
            Assert.AreEqual(a.MultiIndexer[0], a.MultiIndexer[0]);
        }

        [Test]
        public void LensSetIndex3()
        {
            var b = new B().Set(p => p.Numbers, new[] { 0, 1, 2 }.ToImmutableList());

            var index = 0;

            b = b.Set(p => p.Numbers[index], v => v + 100);

            index = 1;

            b = b.Set(p => p.Numbers[index], v => v + 100);

            Assert.AreEqual(new[] { 100, 101, 2 }.ToImmutableList(), b.Numbers);
        }

        [Test]
        public void LensSetIndex4()
        {
            var b = new B().Set(p => p.Cs, new[] { new C(0), new C(1), new C(2) }.ToImmutableList());

            var index = 0;

            b = b.Set(p => p.Cs[index].P, v => v + 100);

            index = 1;

            b = b.Set(p => p.Cs[index].P, v => v + 100);

            Assert.AreEqual(new[] { 100, 101, 2 }.ToImmutableList(), b.Cs.Select(item => item.P));
        }

        [Test]
        public void LensCanSetIndexWithExpression()
        {
            var a = new MultiIndexer();
            var value0 = 5;
            var value1 = 5;
            var result = a.Set(p => p[value1 - value0], 100);
            Assert.AreEqual(100, result[0]);
            Assert.AreEqual(10, a[0]);
        }

        [Test]
        public void LensThrowsNullReferenceCorrectly()
        {
            var a = new A();

            Assert.AreEqual(null, a.B);

            Assert.Throws<NullReferenceException>(() => a.Set(p => p.B.P, 5));
        }

        [Test]
        public void LensCanSetPropertyInIndex()
        {
            var result = new B().Set(p => p.Cs[1].P, 100);

            Assert.AreEqual(100, result.Cs[1].P);
        }

        [TestCase(-1)]
        [TestCase(14)]
        public void LensThrowsExceptionsIfIndexIsOutOfBounds(int index)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new A(b: new B()).Set(p => p.B.Numbers[index], 100));
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

        [Test]
        public void SetManyTest0()
        {
            var a = new A(0, new B());

            var result = a.SetMany(
                new Pav<A, B>(p => p.B, _ => new B(5)), 
                new Pav<A, int>(p => p.P, _ => 42));

            Assert.AreEqual(0, a.P);
            Assert.AreEqual(0, a.B.P);
            Assert.AreEqual(42, result.P);
            Assert.AreEqual(5, result.B.P);
        }

        [Test]
        public void SetManyTest1()
        {
            var a = new A(0, new B());

            var result = a.SetMany(
                new Pav<A, A>(p => p, _ => new A(0, new B())),
                new Pav<A, int>(p => p.P, _ => 42));

            Assert.AreEqual(0, a.P);
            Assert.AreEqual(0, a.B.P);
            Assert.AreEqual(42, result.P);
            Assert.AreEqual(0, result.B.P);
        }

        [Test]
        public void SetManyTest2()
        {
            var a = new A(0, new B());

            var result = a.SetMany(
                new Pav<A, int>(p => p.P, _ => 42),
                new Pav<A, A>(p => p, _ => new A(0, new B())));

            Assert.AreEqual(0, a.P);
            Assert.AreEqual(0, a.B.P);
            Assert.AreEqual(0, result.P);
            Assert.AreEqual(0, result.B.P);
        }

        public interface IThing : IRecord
        {
            int Value { get; }
            string this[int index] { get; }
        }

        public class ThingImplementation : IThing
        {
            public string this[int index]
            {
                get => Texts[index];
                private set => Texts = Texts.SetItem(index, value);
            }
            public ImmutableList<string> Texts { get; private set; } = new[] { "a", "b", "c" }.ToImmutableList();

            public int Value { get; private set; } = 5;
        }

        [Test]
        public void SetUsesImplementedProperty()
        {
            IThing thing = new ThingImplementation();
            var result = thing.Set(p => p.Value, v => v + 1);
            Assert.AreEqual(6, result.Value);
        }

        [Test]
        public void SetUsesImplementedIndexer()
        {
            IThing thing = new ThingImplementation();
            var result = thing.Set(p => p[1], v => v + " changed");
            Assert.AreEqual("b changed", result[1]);
        }

        public class ValidateObject : IState
        {
            public int A { get; private set; }
            public int B { get; private set; }

            public bool IsValid() => A == B;
        }

        [Test]
        public void DisableValidationTest0()
        {
            var validObject = new ValidateObject();
            validObject = validObject.ChangeWithoutValidation(self => self.Set(p => p.A, 5).Set(p => p.B, 5));

            Assert.IsTrue(validObject.IsValid());
        }

        [Test]
        public void DisableValidationTest1()
        {
            var invalidObject = new ValidateObject();
            invalidObject = invalidObject.ChangeWithoutValidation(self => self.Set(p => p.A, 5).Set(p => p.B, 6));

            // We still don't get an exception even when validation would otherwise fail.
            Assert.IsFalse(invalidObject.IsValid());
        }

        [Test]
        public void DisableValidationUpdatesInstance()
        {
            var invalidObject = new ValidateObject();
            invalidObject = invalidObject.ChangeWithoutValidation(self => self.Set(p => p.A, 5).Set(p => p.B, 6));

            Assert.AreEqual(5, invalidObject.A);
            Assert.AreEqual(6, invalidObject.B);
        }

        [Test]
        public void DisableValidationRunsValidationAction()
        {
            var invalidObject = new ValidateObject();
            Assert.Throws<InvalidStateException>(() => invalidObject.ChangeWithoutValidation(
                self => self.Set(p => p.A, 5).Set(p => p.B, 6), 
                self =>
                {
                    if (!self.IsValid())
                    {
                        throw new InvalidStateException(self);
                    }
                }));
        }

        [Test]
        public void DisableValidationEnablesValidationAfterwards()
        {
            var invalidObject = new ValidateObject();
            invalidObject = invalidObject.ChangeWithoutValidation(self => self.Set(p => p.A, 5).Set(p => p.B, 6));

            Assert.Throws<InvalidStateException>(() => invalidObject.Set(p => p.A, 123));
        }

        [Test]
        public void NestedDisableValidationWorks()
        {
            var a = new ValidateObject();
            var c = a.ChangeWithoutValidation(
                self =>
                {
                    var b = a.ChangeWithoutValidation(self2 => self2.Set(p => p.A, 5));
                    Assert.IsTrue(Lens.Lens.ValidationPaused);
                    return b.Set(p => p.B, 6);
                });

            Assert.IsFalse(Lens.Lens.ValidationPaused);
            Assert.AreEqual(5, c.A);
            Assert.AreEqual(6, c.B);
        }

        [Test]
        public void DisableValidationWorksWithException()
        {
            var a = new ValidateObject();
            try
            {
                a.ChangeWithoutValidation(
                    self =>
                    {
                        throw new Exception();
                    });
            }
            catch
            {
            }

            Assert.IsFalse(Lens.Lens.ValidationPaused);
        }
    }
}
