using System;

namespace AttemptController.DataStructures
{
    public struct DecayingDouble
    {
        public DateTime? LastUpdatedUtc { get; private set; }

        public double ValueAtTimeOfLastUpdate { get; private set; }


        public double GetValue(TimeSpan halfLife, DateTime? whenUtc = null)
        {
            return Decay(ValueAtTimeOfLastUpdate, halfLife, LastUpdatedUtc, whenUtc);
        }

        public static double Decay(double valueLastSetTo, TimeSpan halfLife, DateTime? whenLastSetUtc, DateTime? timeToDecayTo = null)
        {
            if (whenLastSetUtc == null)
                return valueLastSetTo;
            DateTime whenUtcTime = timeToDecayTo ?? DateTime.UtcNow;
            if (whenUtcTime <= whenLastSetUtc.Value)
                return valueLastSetTo;
            TimeSpan timeSinceLastUpdate = whenUtcTime - whenLastSetUtc.Value;
            double halfLivesSinceLastUpdate = timeSinceLastUpdate.TotalMilliseconds / halfLife.TotalMilliseconds;
            return valueLastSetTo / Math.Pow(2, halfLivesSinceLastUpdate);
        }

        public void SetValue(double newValue, DateTime? whenUtc = null)
        {
            LastUpdatedUtc = whenUtc;
            ValueAtTimeOfLastUpdate = newValue;
        }

        public void SetValue(DecayingDouble source)
        {
            LastUpdatedUtc = source.LastUpdatedUtc;
            ValueAtTimeOfLastUpdate = source.ValueAtTimeOfLastUpdate;
        }

        public DecayingDouble(double initialValue = 0d, DateTime? initialLastUpdateUtc = null)
        {
            ValueAtTimeOfLastUpdate = initialValue;
            LastUpdatedUtc = initialLastUpdateUtc;
        }

        public DecayingDouble Add(TimeSpan halfLife, DecayingDouble amountToAdd)
        {
            if (!LastUpdatedUtc.HasValue)
            {
                return new DecayingDouble(ValueAtTimeOfLastUpdate + amountToAdd.ValueAtTimeOfLastUpdate, amountToAdd.LastUpdatedUtc);
            }
            else if (!amountToAdd.LastUpdatedUtc.HasValue)
            {
                return new DecayingDouble(ValueAtTimeOfLastUpdate + amountToAdd.ValueAtTimeOfLastUpdate, LastUpdatedUtc);
            }
            else if (LastUpdatedUtc.Value > amountToAdd.LastUpdatedUtc.Value)
            {
                return new DecayingDouble(
                        ValueAtTimeOfLastUpdate + amountToAdd.GetValue(halfLife, LastUpdatedUtc.Value),
                        LastUpdatedUtc.Value);
            }
            else
            {
                return new DecayingDouble(amountToAdd.ValueAtTimeOfLastUpdate + GetValue(halfLife, amountToAdd.LastUpdatedUtc.Value), amountToAdd.LastUpdatedUtc.Value);
            }
        }

        public void AddInPlace(TimeSpan halfLife, double amountToAdd, DateTime? whenToAddIt)
        {
            if (!LastUpdatedUtc.HasValue)
            {
                ValueAtTimeOfLastUpdate += amountToAdd;
                LastUpdatedUtc = whenToAddIt;
            }
            else if (!whenToAddIt.HasValue)
            {
                ValueAtTimeOfLastUpdate += amountToAdd;
            }
            else if (LastUpdatedUtc.Value > whenToAddIt.Value)
            {
                ValueAtTimeOfLastUpdate += Decay(amountToAdd, halfLife, whenToAddIt, LastUpdatedUtc);
            }
            else
            {
                ValueAtTimeOfLastUpdate = GetValue(halfLife, whenToAddIt) +
                                          amountToAdd;
                LastUpdatedUtc = whenToAddIt;
            }
        }

        public void SubtractInPlace(TimeSpan halfLife, double amountToSubtract, DateTime? whenToSubtractIt)
        {
            if (!LastUpdatedUtc.HasValue)
            {
                ValueAtTimeOfLastUpdate -= amountToSubtract;
                LastUpdatedUtc = whenToSubtractIt;
            }
            else if (!whenToSubtractIt.HasValue)
            {
                ValueAtTimeOfLastUpdate -= amountToSubtract;
            }
            else if (LastUpdatedUtc.Value > whenToSubtractIt.Value)
            {
                ValueAtTimeOfLastUpdate -= Decay(amountToSubtract, halfLife, whenToSubtractIt, LastUpdatedUtc);
            }
            else
            {
                ValueAtTimeOfLastUpdate = GetValue(halfLife, whenToSubtractIt) -
                                          amountToSubtract;
                LastUpdatedUtc = whenToSubtractIt;
            }
        }


        public void AddInPlace(TimeSpan halfLife, DecayingDouble amountToAdd)
            => AddInPlace(halfLife, amountToAdd.ValueAtTimeOfLastUpdate, amountToAdd.LastUpdatedUtc);

        public void SubtractInPlace(TimeSpan halfLife, DecayingDouble amountToSubtract)
            => SubtractInPlace(halfLife, amountToSubtract.ValueAtTimeOfLastUpdate, amountToSubtract.LastUpdatedUtc);


        public DecayingDouble Subtract(TimeSpan halfLife, DecayingDouble amountToRemove)
        {
            if (!LastUpdatedUtc.HasValue)
            {
                return new DecayingDouble(ValueAtTimeOfLastUpdate - amountToRemove.ValueAtTimeOfLastUpdate, amountToRemove.LastUpdatedUtc);
            }
            else if (!amountToRemove.LastUpdatedUtc.HasValue)
            {
                return new DecayingDouble(ValueAtTimeOfLastUpdate - amountToRemove.ValueAtTimeOfLastUpdate, LastUpdatedUtc);
            } else if (LastUpdatedUtc.Value > amountToRemove.LastUpdatedUtc.Value)
            {
                return
                    new DecayingDouble(
                        ValueAtTimeOfLastUpdate - amountToRemove.GetValue(halfLife, LastUpdatedUtc.Value),
                        LastUpdatedUtc.Value);
            }
            else
            {
                return new DecayingDouble(amountToRemove.ValueAtTimeOfLastUpdate - GetValue(halfLife, amountToRemove.LastUpdatedUtc.Value), amountToRemove.LastUpdatedUtc.Value);
            }
        }

        public void Add(double amountToAdd, TimeSpan halfLife, DateTime? timeOfAddOperationUtc = null)
        {
            SetValue(GetValue(halfLife, timeOfAddOperationUtc) + amountToAdd, timeOfAddOperationUtc);
        }
    }
}

