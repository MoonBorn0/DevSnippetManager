using System;
using System.Collections.Generic;
using System.Linq;

namespace Demo.Core
{
    /// <summary>Hesap makinesi.</summary>
    public class Calculator
    {
        private readonly List<int> _history = new();

        /// <summary>
        /// İki sayıyı toplar.
        /// </summary>
        /// <param name="a">Birinci sayı</param>
        /// <param name="b">İkinci sayı</param>
        /// <returns>Toplam</returns>
        public int Add(int a, int b)
        {
            var total = a + b;
            _history.Add(total);
            return total;
        }

        // Bölme işlemi.
        // Sıfıra bölmeyi denetler.
        public double Divide(double a, double b)
        {
            if (b == 0) throw new DivideByZeroException("b }");
            return a / b;
        }

        public string Format(int x)
        {
            var s = $"{x} değeri {{süslü}} ve {(x > 0 ? "pozitif }" : "negatif {")}";
            var raw = @"verbatim } içinde ""tırnak"" {";
            /* blok yorum } { */
            return s + raw;
        }

        public int Add(int a, int b, int c)
        {
            int Helper(int v)
            {
                return v * 2;
            }
            return Helper(a) + b + c;
        }

        public int Count => _history.Count;

        public int Last
        {
            get
            {
                if (_history.Count == 0) return -1;
                return _history[^1];
            }
        }

        public int this[int i]
        {
            get { return _history[i]; }
            set { _history[i] = value; }
        }

        public static Calculator operator +(Calculator x, Calculator y)
        {
            var c = new Calculator();
            c._history.AddRange(x._history);
            c._history.AddRange(y._history);
            return c;
        }

        public Calculator()
        {
            _history.Clear();
            _history.Capacity = 10;
        }

        public class Inner
        {
            public void Run()
            {
                Console.WriteLine("inner");
                Console.WriteLine("run");
            }

            public class Deep
            {
                public int Compute<T>(T value) where T : struct
                {
                    var x = value.GetHashCode();
                    return x;
                }
            }
        }

        #region Regioned
        public async System.Threading.Tasks.Task<int> LoadAsync(string path, System.Threading.CancellationToken ct = default)
        {
            await System.Threading.Tasks.Task.Delay(1, ct);
            return path.Length;
        }
        #endregion
    }

    public enum Mode { A, B, C }

    public interface IShape
    {
        double Area();
        double Perimeter();
    }

    public record Point(int X, int Y)
    {
        public double Dist() => Math.Sqrt(X * X + Y * Y);
    }

    public class OnlyFields
    {
        public int A;
        public int B;
        public string C = "x";
    }

    public abstract class Shape
    {
        public abstract double Area();
        public virtual string Name => "shape";
    }
}
