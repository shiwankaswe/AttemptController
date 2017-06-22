using System.Collections.Generic;
using System.Linq;

namespace AttemptController.DataStructures
{
    public struct Proportion
    {
        public ulong Numerator { get; }
        public ulong Denominator { get; }

        public double AsDouble { get; }
        

        public Proportion(ulong numerator, ulong denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
            AsDouble = Denominator == 0 ? 0 : Numerator / ((double)Denominator);
        }
        

        public Proportion MinDenominator(ulong minDenominator)
        {
            return Denominator >= minDenominator ? this : new Proportion(Numerator, minDenominator);
        }

        public static Proportion GetLargest(IEnumerable<Proportion> proportions)
        {
            Proportion max = new Proportion(0,ulong.MaxValue);
            foreach (Proportion p in proportions)
            {
                if (p.AsDouble > max.AsDouble)
                    max = p;
            }
            return max;
        }

    }
}
