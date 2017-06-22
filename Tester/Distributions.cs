using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AttemptController.EncryptionPrimitives;

namespace Tester
{
    public static class Distributions
    {
        public static double GetLogNormal(double mu, double sigma)
        {
            double x = Math.Exp(mu + sigma * GetNormal());
            return x;
        }

        public static double GetNormal()
        {
            double v1;
            double s;
            do
            {
                v1 = 2.0 * StrongRandomNumberGenerator.GetFraction() - 1.0;
                double v2 = 2.0 * StrongRandomNumberGenerator.GetFraction() - 1.0;
                s = v1 * v1 + v2 * v2;
            } while (s >= 1d || s <= 0d);

            s = Math.Sqrt((-2.0 * Math.Log(s)) / s);
            return v1 * s;
        }

    }
}
