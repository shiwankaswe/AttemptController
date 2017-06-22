﻿using System;
using System.Collections.Generic;
using System.Linq;
using AttemptController.EncryptionPrimitives;

namespace Tester
{
    public class WeightedSelector<T>
    {
        private readonly List<T> _items = new List<T>();

        private readonly List<double> _cumulativeWeight = new List<double>();

        public void AddItem(T item, double weight)
        {
            _items.Add(item);
            _cumulativeWeight.Add(weight + (_cumulativeWeight.Count > 0 ? _cumulativeWeight[_cumulativeWeight.Count-1] : 0));
        }

        public List<T> GetItems(int count = Int32.MaxValue)
        {
            count = Math.Min(count, _items.Count);
            return _items.Take(count).ToList();
        }

        public WeightedSelector<T> TrimToInitialItems(int count)
        {
            WeightedSelector<T> trimmed = new WeightedSelector<T>();
            count = Math.Min(count, _items.Count);
            trimmed._items.AddRange(_items.Take(count));
            trimmed._cumulativeWeight.AddRange(_cumulativeWeight.Take(count));
            return trimmed;
        }

        public WeightedSelector<T> TrimToRemoveInitialItems(int count)
        {
            WeightedSelector<T> trimmed = new WeightedSelector<T>();
            count = Math.Min(count, _items.Count);
            double weightRemoved = _cumulativeWeight[count];
            trimmed._items.AddRange(_items.Skip(count));
            trimmed._cumulativeWeight.AddRange(_cumulativeWeight.Skip(count).Select( x=> x-weightRemoved ));
            return trimmed;
        }

        public T GetItemByWeightedRandom()
        {
            if (_cumulativeWeight.Count == 0)
                return default(T);
            double randomValueLessThanWeight =
                StrongRandomNumberGenerator.GetFraction() *_cumulativeWeight[_cumulativeWeight.Count - 1];

            int minIndex = 0;
            int maxIndex = _cumulativeWeight.Count - 1;
            while (maxIndex > minIndex)
            {
                int midPointIndex = (maxIndex + minIndex) / 2;
                double midPointValue = _cumulativeWeight[midPointIndex];
                if (midPointValue < randomValueLessThanWeight)
                {
                    minIndex = midPointIndex + 1;
                }
                else
                {
                    maxIndex = midPointIndex;
                }
            }

            return _items[minIndex];
        }
    }
}
