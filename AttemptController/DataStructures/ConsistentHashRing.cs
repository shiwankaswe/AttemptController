using System;
using System.Collections.Generic;
using System.Linq;
using AttemptController.EncryptionPrimitives;
using AttemptController.Interfaces;

namespace AttemptController.DataStructures
{

    public class ConsistentHashRing<TMember> : IDistributedResponsibilitySet<TMember>
    {

        readonly HashSet<string> _membersKeys;

        internal Dictionary<ulong, KeyValuePair<string, TMember>> PointsToMembers;

        internal List<ulong> SortedPoints;

        readonly int _numberOfPointsOnRingForEachMember;

        readonly UniversalHashFunction _baseHashFunction;

        readonly UniversalHashFunction[] _universalHashFunctionsForEachPoint;

        readonly System.Threading.ReaderWriterLockSlim _readWriteLock = new System.Threading.ReaderWriterLockSlim(System.Threading.LockRecursionPolicy.NoRecursion);


        int _numberOfMembers;

        public int Count => _numberOfMembers;

        const int DefaultMaxInputLengthInBytesForUniversalHashFunction = 256;
        const int DefaultNumberOfPointsOnRingForEachMember = 512;

        public ConsistentHashRing(string key,
                                   IEnumerable<KeyValuePair<string, TMember>> initialMembers = null,
                                   int maxInputLengthInBytesForUniversalHashFunction = DefaultMaxInputLengthInBytesForUniversalHashFunction,
                                   int numberOfPointsOnRingForEachMember = DefaultNumberOfPointsOnRingForEachMember)
        {
            _numberOfPointsOnRingForEachMember = numberOfPointsOnRingForEachMember;

            _membersKeys = new HashSet<string>();
            PointsToMembers = new Dictionary<ulong, KeyValuePair<string, TMember>>();
            SortedPoints = new List<ulong>();

            _baseHashFunction = new UniversalHashFunction(key, maxInputLengthInBytesForUniversalHashFunction);
            _universalHashFunctionsForEachPoint = new UniversalHashFunction[numberOfPointsOnRingForEachMember];
            for (int i = 0; i < _universalHashFunctionsForEachPoint.Length; i++)
            {
                _universalHashFunctionsForEachPoint[i] = new UniversalHashFunction(key + ":" + i.ToString(), 16);
            }

            if (initialMembers != null)
                AddRange(initialMembers);
        }


        internal ulong[] GetPointsForMember(string membersKey)
        {
            ulong[] points = new ulong[_numberOfPointsOnRingForEachMember];
            ulong initialHash = _baseHashFunction.Hash(membersKey, UniversalHashFunction.MaximumNumberOfResultBitsAllowing32BiasedBits);
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = _universalHashFunctionsForEachPoint[i].Hash(initialHash, UniversalHashFunction.MaximumNumberOfResultBitsAllowing32BiasedBits);
            }
            return points;
        }



        public bool ContainsKey(string key)
        {
            _readWriteLock.EnterReadLock();
            try
            {
                return _membersKeys.Contains(key);
            }
            finally
            {
                _readWriteLock.ExitReadLock();
            }
        }

        public void Add(string uniqueKeyIdentifiyingMember, TMember member)
        {
            _readWriteLock.EnterWriteLock();
            try
            {
                if (!_membersKeys.Contains(uniqueKeyIdentifiyingMember))
                {
                    _membersKeys.Add(uniqueKeyIdentifiyingMember);

                    _numberOfMembers++;

                    ulong[] points = GetPointsForMember(uniqueKeyIdentifiyingMember);

                    foreach (ulong point in points)
                    {
                        PointsToMembers[point] = new KeyValuePair<string, TMember>(uniqueKeyIdentifiyingMember, member);
                    }

                    SortedPoints.AddRange(points);

                    SortedPoints.Sort();
                }
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }
        }

        public void AddRange(IEnumerable<KeyValuePair<string, TMember>> newKeyMemberPairs)
        {
            _readWriteLock.EnterWriteLock();
            try
            {
                foreach (KeyValuePair<string, TMember> keyAndMember in newKeyMemberPairs)
                {
                    if (!_membersKeys.Contains(keyAndMember.Key))
                    {
                        _membersKeys.Add(keyAndMember.Key);

                        _numberOfMembers++;

                        ulong[] points = GetPointsForMember(keyAndMember.Key);
                        foreach (ulong point in points)
                        {
                            PointsToMembers[point] = keyAndMember;
                        }
                        SortedPoints.AddRange(points);
                    }
                }
                SortedPoints.Sort();
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }
        }



        protected void RemoveFromSortedPoints_WithoutLocking(IEnumerable<ulong> pointsToRemove)
        {
            List<ulong> sortedPointsToRemove = new List<ulong>(pointsToRemove);
            sortedPointsToRemove.Sort();

            foreach (ulong point in sortedPointsToRemove)
                PointsToMembers.Remove(point);


            List<ulong> newSortedPoints = new List<ulong>(Math.Max(0, SortedPoints.Count - sortedPointsToRemove.Count));

            int indexIntoPointsToRemove = 0;
            for (int indexIntoAllPoints = 0; indexIntoAllPoints < SortedPoints.Count;)
            {
                if (indexIntoPointsToRemove >= sortedPointsToRemove.Count)
                {
                    newSortedPoints.Add(SortedPoints[indexIntoAllPoints++]);
                }
                else if (SortedPoints[indexIntoAllPoints] == sortedPointsToRemove[indexIntoPointsToRemove])
                {
                    indexIntoAllPoints++;
                }
                else if (SortedPoints[indexIntoAllPoints] < sortedPointsToRemove[indexIntoPointsToRemove])
                {
                    newSortedPoints.Add(SortedPoints[indexIntoAllPoints++]);
                }
                else
                {
                }

                SortedPoints = newSortedPoints;

            }
        }


        public void Remove(string uniqueKeyIdentifiyingMember)
        {
            _readWriteLock.EnterWriteLock();
            try
            {
                if (_membersKeys.Contains(uniqueKeyIdentifiyingMember))
                {
                    _membersKeys.Remove(uniqueKeyIdentifiyingMember);

                    RemoveFromSortedPoints_WithoutLocking(GetPointsForMember(uniqueKeyIdentifiyingMember));
                }            
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }
        }

        public void RemoveRange(IEnumerable<string> uniqueKeysIdentifiyingMember)
        {
            List<ulong> pointsToRemove = new List<ulong>();

            _readWriteLock.EnterWriteLock();
            try
            {
                foreach (string uniqueKeyIdentifiyingMember in uniqueKeysIdentifiyingMember) {
                    if (_membersKeys.Contains(uniqueKeyIdentifiyingMember))
                    {
                        _membersKeys.Remove(uniqueKeyIdentifiyingMember);

                        pointsToRemove.AddRange(GetPointsForMember(uniqueKeyIdentifiyingMember));

                    }
                }

                RemoveFromSortedPoints_WithoutLocking(pointsToRemove);
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }
        }


        public Dictionary<string, double> FractionalCoverage
        {
            get
            {
                Dictionary<string, ulong> counts = new Dictionary<string, ulong>();
                ulong lastPoint = 0;
                counts[PointsToMembers[SortedPoints[0]].Key] = (lastPoint - SortedPoints[SortedPoints.Count - 1]) + 1;
                foreach (ulong point in SortedPoints)
                {
                    ulong sinceLast = point - lastPoint;
                    string key = PointsToMembers[point].Key;
                    if (!counts.ContainsKey(key))
                        counts[key] = 0;
                    counts[key] += sinceLast;
                    lastPoint = point;
                }
                List<string> keys = counts.Keys.ToList();
                Dictionary<string, double> result = new Dictionary<string, double>();
                foreach (string key in keys)
                    result[key] = counts[key] / (double)ulong.MaxValue;
                return result;
            }
        }

        private TMember BinarySearchWithoutLocking(ulong pointToFind) 
        {            
            int minIndex = 0;
            int maxIndex = SortedPoints.Count - 1;

            if (pointToFind > SortedPoints[maxIndex])
            {
                return PointsToMembers[SortedPoints[0]].Value;
            }
            else
            {
                while (maxIndex > minIndex)
                {
                    int midPointIndex = (maxIndex + minIndex) / 2;
                    ulong midPointValue = SortedPoints[midPointIndex];
                    if (midPointValue < pointToFind)
                    {
                        minIndex = midPointIndex + 1;
                    }
                    else
                    {
                        maxIndex = midPointIndex;
                    }
                }
                if (pointToFind > SortedPoints[0] && SortedPoints[minIndex] < pointToFind)
                    throw new Exception("Illegal");

                return PointsToMembers[SortedPoints[minIndex]].Value;
            }

        }

        public TMember FindMemberResponsible(string key)
        {
            ulong pointToFind = _baseHashFunction.Hash("0:" + key, UniversalHashFunction.MaximumNumberOfResultBitsAllowing32BiasedBits);

            TMember memberWithPointClosestToPointToFind;

            _readWriteLock.EnterReadLock();
            try
            {
                memberWithPointClosestToPointToFind = BinarySearchWithoutLocking(pointToFind);
            }
            finally
            {
                _readWriteLock.ExitReadLock();
            }
        
            return memberWithPointClosestToPointToFind;
        }

        public List<TMember> FindMembersResponsible(string key, int numberOfUniqueMembersToFind)
        {
            List<TMember> result = new List<TMember>(numberOfUniqueMembersToFind);

            _readWriteLock.EnterReadLock();
            try
            {
                while (result.Count < numberOfUniqueMembersToFind && result.Count < _numberOfMembers) {
                    ulong pointToFind = _baseHashFunction.Hash(result.Count.ToString() + ":" + key, UniversalHashFunction.MaximumNumberOfResultBitsAllowing32BiasedBits);
                    TMember member = BinarySearchWithoutLocking(pointToFind);
                    if (!result.Contains(member))
                        result.Add(member);
                }
            }
            finally
            {
                _readWriteLock.ExitReadLock();
            }

            return result;
        }



    }
}
