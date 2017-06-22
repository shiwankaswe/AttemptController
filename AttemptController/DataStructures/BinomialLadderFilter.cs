using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AttemptController.Clients;
using AttemptController.EncryptionPrimitives;
using AttemptController.Interfaces;

namespace AttemptController.DataStructures
{
    public class FilterArray
    {
        protected readonly BitArray BitArray;

        protected readonly UniversalHashFunction[] HashFunctionsMappingElementsToBitsInTheArray;

        public int Length => BitArray.Length;

        public int MaximumBitIndexesPerElement => HashFunctionsMappingElementsToBitsInTheArray.Length;

        public FilterArray(int numberOfBitsInArray, int maximumBitIndexesPerElement, bool initilizeBitsOfArrayAtRandom,
            string saltForHashFunctions = "")
        {
            int capacityInBytes = (numberOfBitsInArray + 7) / 8;

            HashFunctionsMappingElementsToBitsInTheArray = new UniversalHashFunction[maximumBitIndexesPerElement];
            for (int i = 0; i < HashFunctionsMappingElementsToBitsInTheArray.Length; i++)
            {
                HashFunctionsMappingElementsToBitsInTheArray[i] =
                    new UniversalHashFunction(i + ":" + saltForHashFunctions, 64);
            }

            if (initilizeBitsOfArrayAtRandom)
            {
                byte[] initialBitValues = new byte[capacityInBytes];
                StrongRandomNumberGenerator.GetBytes(initialBitValues);
                BitArray = new BitArray(initialBitValues);
            }
            else
            {
                BitArray = new BitArray(capacityInBytes * 8);
            }
        }

        public IEnumerable<int> GetIndexesAssociatedWithAnElement(byte[] element, int? numberOfIndexesRequested = null)
        {
            HashSet<int> indexesIntoBitArray = new HashSet<int>();

            int numberOfBitIndexesToCreate = Math.Min(HashFunctionsMappingElementsToBitsInTheArray.Length, numberOfIndexesRequested ?? int.MaxValue);

            for (int i = 0; i < numberOfBitIndexesToCreate; i++)
            {
                UniversalHashFunction hashFunction = HashFunctionsMappingElementsToBitsInTheArray[i];
                byte[] valueToHash = ManagedSHA256.Hash(element);
                do
                {
                    int indexIntoBitArray = (int)(hashFunction.Hash(valueToHash) % (uint)Length);
                    if (indexesIntoBitArray.Add(indexIntoBitArray))
                    {
                        break;
                    }
                    valueToHash = ManagedSHA256.Hash(valueToHash);
                } while (true);
            }

            return indexesIntoBitArray;
        }

        public IEnumerable<int> GetIndexesAssociatedWithAnElement(string element, int? numberOfIndexesRequested = null)
        {
            return GetIndexesAssociatedWithAnElement(Encoding.UTF8.GetBytes(element), numberOfIndexesRequested);
        }

        public bool SetBitToOne(int indexOfBitToSet) => AssignBit(indexOfBitToSet, true);
        public bool ClearBitToZero(int indexOfBitToClear) => AssignBit(indexOfBitToClear, false);

        public bool this[int index]
        {
            get { return BitArray[index]; }
            set { BitArray[index] = value; }
        }

        public bool AssignBit(int indexOfTheBitToAssign, bool desiredValue)
        {
            bool result = BitArray[indexOfTheBitToAssign] != desiredValue;
            if (result)
            {
                BitArray[indexOfTheBitToAssign] = desiredValue;
            }
            return result;
        }

        public void ClearRandomBitToZero()
        {
            ClearBitToZero((int)StrongRandomNumberGenerator.Get32Bits((uint)BitArray.Length));
        }

        public void SetRandomBitToOne()
        {
            SetBitToOne((int)StrongRandomNumberGenerator.Get32Bits((uint)BitArray.Length));
        }

        public void AssignRandomBit(int value)
        {
            AssignBit((int)StrongRandomNumberGenerator.Get32Bits((uint)BitArray.Length), value != 0);
        }

        public void SwapBits(int indexA, int indexB)
        {
            bool tmp = BitArray[indexA];
            BitArray[indexA] = BitArray[indexB];
            BitArray[indexB] = tmp;
        }

    }

    public class BinomialLadderFilter : FilterArray, IBinomialLadderFilter
    {
        public int MaxHeight => base.MaximumBitIndexesPerElement;

        public BinomialLadderFilter(int numberOfRungsInArray, int maxLadderHeightInRungs) : base(numberOfRungsInArray, maxLadderHeightInRungs, true)
        {
        }

        public int GetHeight(string key, int? heightOfLadderInRungs = null)
        {
            return GetIndexesAssociatedWithAnElement(key, heightOfLadderInRungs).Count(rung => this[rung]);
        }

        protected int GetIndexOfRandomBitOfDesiredValue(bool desiredValueOfElement)
        {
            int elementIndex;
            do
            {
                elementIndex = (int)(StrongRandomNumberGenerator.Get64Bits((ulong)base.Length));
            } while (base[elementIndex] != desiredValueOfElement);

            return elementIndex;
        }

        public int Step(string key, int? heightOfLadderInRungs = null)
        {
            List<int> rungs = GetIndexesAssociatedWithAnElement(key, heightOfLadderInRungs).ToList();
            List<int> rungsAbove = rungs.Where(rung => !base[rung]).ToList();

            int indexOfElementToSet = (rungsAbove.Count > 0) ?
                rungsAbove[(int) (StrongRandomNumberGenerator.Get32Bits((uint) rungsAbove.Count))] :
                GetIndexOfRandomBitOfDesiredValue(false);

            int indexOfElementToClear = GetIndexOfRandomBitOfDesiredValue(true);

            SwapBits(indexOfElementToSet, indexOfElementToClear);

            return rungs.Count - rungsAbove.Count;
        }

        public async Task<int> StepAsync(string key, int? heightOfLadderInRungs = null, TimeSpan? timeout = null,
            CancellationToken cancellationToken = new CancellationToken())
            => Step(key, heightOfLadderInRungs);

        public async Task<int> GetHeightAsync(string element, int? heightOfLadderInRungs = null, TimeSpan? timeout = null,
            CancellationToken cancellationToken = new CancellationToken()) => GetHeight(element, heightOfLadderInRungs);

    }

}
