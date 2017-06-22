using System;
using System.Security.Cryptography;
using System.Text;
using AttemptController.EncryptionPrimitives;
using System.Threading.Tasks;

namespace AttemptController.DataStructures
{
    public class Sketch
    {
        public long NumberOfColumns { get; }

        public long NumberOfRows { get; }

        protected ArrayOfUnsignedNumericOfNonstandardSize[] Columns {get; }

        protected ulong[] ColumnTotals { get; }
 
        public int BitsPerElement { get; private set; } 

        protected ulong MaxValue {get; }

        public bool IsTheNumberOfRowsAPowerOfTwo { get; }

        public long RowIndexMaskForPowersOfTwo { get; }

        public int HashBytesPerRowIndex { get; }


        public Sketch(long numberOfColumns, long numberOfRows, int bitsPerElement)
        {
            NumberOfColumns = numberOfColumns;
            NumberOfRows = numberOfRows;
            BitsPerElement = bitsPerElement;

            MaxValue = ((((ulong)1) << bitsPerElement) - 1);

            Columns = new ArrayOfUnsignedNumericOfNonstandardSize[numberOfColumns];
            for (long i = 0; i < numberOfColumns; i++)
                Columns[i] = ArrayOfUnsignedNumericOfNonstandardSize.Create(bitsPerElement, numberOfRows);
            ColumnTotals = new ulong[numberOfColumns];


            IsTheNumberOfRowsAPowerOfTwo = ( (numberOfRows - 1) & numberOfRows ) == 0;

            int hashBitsPerRowIndex = 0;
            for (long shiftedNumberOfRows = numberOfRows; shiftedNumberOfRows > 0; shiftedNumberOfRows = shiftedNumberOfRows >> 1)
                hashBitsPerRowIndex++;

            if (IsTheNumberOfRowsAPowerOfTwo)
            {
                hashBitsPerRowIndex -= 1;
                RowIndexMaskForPowersOfTwo = numberOfRows-1;
            } else {
                hashBitsPerRowIndex += 10;
            }
            HashBytesPerRowIndex = (hashBitsPerRowIndex + 7) / 8;      
         }

        protected long[] GetIndexesForString(string s) {
            long[] indexes = new long[NumberOfColumns];

            int hashCount = 0;
            
            byte[] hashBytes = ManagedSHA256.Hash(Encoding.UTF8.GetBytes((hashCount++).ToString() + s));

            int hashBytesConsumed = 0;

            for (long i = 0; i < NumberOfColumns; i++) {
                long numberBuiltFromHashBytes = 0;
                for (int j = 0; j < HashBytesPerRowIndex; j++)
                {
                    if (hashBytesConsumed >= hashBytes.Length)
                    {
                        hashBytes = ManagedSHA256.Hash(Encoding.UTF8.GetBytes((hashCount++).ToString() + s));
                        hashBytesConsumed = 0;
                    }
                    numberBuiltFromHashBytes = (numberBuiltFromHashBytes << 8) | hashBytes[hashBytesConsumed++];
                }

                if (IsTheNumberOfRowsAPowerOfTwo)
                {
                    indexes[i] = numberBuiltFromHashBytes & RowIndexMaskForPowersOfTwo;
                }
                else
                {
                    indexes[i] = numberBuiltFromHashBytes % NumberOfRows;
                }
            }

            return indexes;            
        }


        public ulong GetColumnTotal(int column)
        {
            return ColumnTotals[column];
        }


        public class ResultOfGet
        {
            public ulong Min { get;  }
            public ulong Max { get; }
            public ulong MinColumnTotal { get;  }
            public ulong MaxColumnTotal { get; }
            public Proportion Proportion => new Proportion(Min, MinColumnTotal);

            public ResultOfGet(ulong min, ulong max, ulong minColumnTotal, ulong maxColumnTotal)
            {
                Min = min;
                Max = max;
                MinColumnTotal = minColumnTotal;
                MaxColumnTotal = maxColumnTotal;
            }
        }

        public class ResultOfUpdate
        {
            public ulong PriorMin { get; }
            public ulong PriorMax { get; }
            public ulong PriorMinColumnTotal { get; }
            public ulong PriorMaxColumnTotal { get; }
            public ulong NewMin { get; }
            public ulong NewMax { get; }
            public ulong NewMinColumnTotal { get; }
            public ulong NewMaxColumnTotal { get; }

            public Proportion PriorProportion => new Proportion(PriorMin, PriorMinColumnTotal);
            public Proportion NewProportion => new Proportion(NewMin, NewMinColumnTotal);

            public ResultOfUpdate(ulong priorMin, ulong priorMax, ulong priorMinColumnTotal, ulong priorMaxColumnTotal, ulong newMin, ulong newMax, ulong newMinColumnTotal, ulong newMaxColumnTotal)
            {
                PriorMin = priorMin;
                PriorMax = priorMax;
                PriorMinColumnTotal = priorMinColumnTotal;
                PriorMaxColumnTotal = priorMaxColumnTotal;
                NewMin = newMin;
                NewMax = newMax;
                NewMinColumnTotal = newMinColumnTotal;
                NewMaxColumnTotal = newMaxColumnTotal;
            }
        }

        public ResultOfGet Get(long[] elementIndexes)
        {
            ulong min = ulong.MaxValue;
            ulong max = ulong.MinValue;
            ulong minColumnTotal = ulong.MaxValue;
            ulong maxColumnTotal = ulong.MinValue;
            for (int i = 0; i < elementIndexes.Length; i++)
            {                
                ulong columnValue = Read(i, elementIndexes[i]);

                min = Math.Min(min, columnValue);
                max = Math.Max(max, columnValue);

                ulong columnTotal = GetColumnTotal(i);

                minColumnTotal = Math.Min(minColumnTotal, columnTotal);
                maxColumnTotal = Math.Max(maxColumnTotal, columnTotal);
            }
            return new ResultOfGet(min, max, minColumnTotal, maxColumnTotal);
        }

        public ResultOfGet Get(string s)
        {
            return Get(GetIndexesForString(s));
        }


        public ulong GetMin(string s)
        {
            return Get(GetIndexesForString(s)).Min;
        }

        public bool IsNonZero(string s)
        {
            return GetMin(s) > 0;
        }


        public ulong this[string s]
        {
            get
            {
                return this[GetIndexesForString(s)];
            }
            set
            {
                this[GetIndexesForString(s)] = value;
            }
        }

        public ulong this[long[] elementIndexes]
        {
            get
            {
                return Get(elementIndexes).Min;
            }
            set
            {
                for (long i = 0; i < elementIndexes.Length; i++)
                    Write(i, elementIndexes[i], value);
            }
        }

        public ResultOfUpdate Increment(string s)
        {
            return Add(GetIndexesForString(s));
        }

        public ResultOfUpdate ConservativeIncrement(string s)
        {
            return ConservativeAdd(GetIndexesForString(s));
        }


        public ResultOfUpdate Add(string s, ulong amountToAdd = 1)
        {
            return Add(GetIndexesForString(s), amountToAdd);
        }

        public ResultOfUpdate ConservativeAdd(string s, ulong amountToAdd = 1)
        {
            return ConservativeAdd(GetIndexesForString(s), amountToAdd);
        }


        protected virtual void Write(long column, long row, ulong value)
        {
            if (value > MaxValue)
            {
                value = MaxValue;
            }
            ulong oldValue = Columns[column][row];
            if (value != oldValue)
            {
                Columns[column][row] = value;
                ColumnTotals[column] += value - oldValue;
            }
        }

        protected virtual ulong Read(long column, long row)
        {
            return Columns[column][row];
        }


        protected ResultOfUpdate Add(long[] elementIndexForEachColumn, ulong amountToAdd = 1)
        {
            ulong originalMin = ulong.MaxValue;
            ulong originalMax = ulong.MinValue;
            ulong originalMinColumnTotal = ulong.MaxValue;
            ulong originalMaxColumnTotal = ulong.MinValue;
            ulong newMin = ulong.MaxValue;
            ulong newMax = ulong.MinValue;
            ulong newMinColumnTotal = ulong.MaxValue;
            ulong newMaxColumnTotal = ulong.MinValue;

            for (int column = 0; column < elementIndexForEachColumn.Length; column++)
            {
                long row = elementIndexForEachColumn[column];

                ulong oldValue = Read(column, row);

                originalMin = Math.Min(originalMin, oldValue);
                originalMax = Math.Max(originalMax, oldValue);

                ulong columnTotal = GetColumnTotal(column);

                originalMinColumnTotal = Math.Min(originalMinColumnTotal, columnTotal);
                originalMaxColumnTotal = Math.Max(originalMinColumnTotal, columnTotal);

                ulong newValue = Math.Min(oldValue + amountToAdd, MaxValue);
                if (newValue > oldValue)
                {
                    Write(column, row, newValue);

                    columnTotal = GetColumnTotal(column);
                }

                newMin = Math.Min(newMin, newValue);
                newMax = Math.Max(newMax, newValue);
                newMinColumnTotal = Math.Min(newMinColumnTotal, columnTotal);
                newMaxColumnTotal = Math.Min(newMaxColumnTotal, columnTotal);
            }

            return new ResultOfUpdate(originalMin, originalMax, originalMinColumnTotal, originalMaxColumnTotal,
                newMin, newMax, newMinColumnTotal, newMaxColumnTotal);
        }


        protected ResultOfUpdate ConservativeAdd(long[] elementIndexForEachColumn, ulong amountToAdd = 1)
        {
            ulong originalMin = ulong.MaxValue;
            ulong originalMax = ulong.MinValue;
            ulong originalMinColumnTotal = ulong.MaxValue;
            ulong originalMaxColumnTotal = ulong.MinValue;
            ulong newMinColumnTotal = ulong.MaxValue;
            ulong newMaxColumnTotal = ulong.MinValue;

            ulong[] values = new ulong[elementIndexForEachColumn.Length];
            for (int column = 0; column < elementIndexForEachColumn.Length; column++)
            {
                ulong value = Read(column, elementIndexForEachColumn[column]);

                originalMin = Math.Min(originalMin, value);
                originalMax = Math.Max(originalMax, value);

                ulong columnTotal = GetColumnTotal(column);

                originalMinColumnTotal = Math.Min(originalMinColumnTotal, columnTotal);
                originalMaxColumnTotal = Math.Max(originalMinColumnTotal, columnTotal);

                values[column] = value;
            }

            if (originalMin >= MaxValue)
            {
                return new ResultOfUpdate(originalMin, originalMax, originalMinColumnTotal, originalMaxColumnTotal,
                    originalMin, originalMax, originalMinColumnTotal, originalMaxColumnTotal);
            }

            ulong newMin = Math.Min(originalMin + amountToAdd, MaxValue);
            ulong newMax = Math.Max(originalMax, newMin);

            for (int column = 0; column < elementIndexForEachColumn.Length; column++)
            {
                if (values[column] < newMin)
                {
                    Write(column, elementIndexForEachColumn[column], newMin);
                }

                ulong columnTotal = GetColumnTotal(column);

                newMinColumnTotal = Math.Min(newMinColumnTotal, columnTotal);
                newMaxColumnTotal = Math.Min(newMaxColumnTotal, columnTotal);
            }

            return new ResultOfUpdate(originalMin, originalMax, originalMinColumnTotal, originalMaxColumnTotal,
                newMin, newMax, newMinColumnTotal, newMaxColumnTotal);
        }

    }

    public class AgingSketch : Sketch
    {
        readonly ulong[] _numberElementsZero;
        readonly long[] _agingIndex;
        readonly Task[] _columnAgingTasks;
        readonly ulong _rowZerosMin;
        readonly ulong _rowZerosMax;

        const float DefaultFractionRowZerosLowWaterMark = .45f;
        const float DefaultFractionRowZerosHighWaterMark = .55f;

        public AgingSketch(long numberOfColumns, long numberOfRows, int bitsPerElement,
            float fractionRowZerosLowWaterMark = DefaultFractionRowZerosLowWaterMark,
            float fractionRowZerosHighWaterMark = DefaultFractionRowZerosHighWaterMark)
            : base(numberOfColumns, numberOfRows, bitsPerElement)
        {
            _columnAgingTasks = new Task[numberOfColumns];
            _agingIndex = new long[numberOfColumns];
            _numberElementsZero = new ulong[numberOfColumns];
            _rowZerosMin = (ulong)(numberOfRows * fractionRowZerosLowWaterMark);
            _rowZerosMax = (ulong)(numberOfRows * fractionRowZerosHighWaterMark);
            for (long i = 0; i < numberOfColumns; i++)
            {
                _numberElementsZero[i] = (ulong)numberOfRows;
            }
        }

        protected override void Write(long column, long row, ulong value)
        {
            ulong newValue = value > MaxValue ? MaxValue : value;
            lock (Columns[column])
            {
                ulong oldValue = Read(column, row);
                base.Write(column, row, value);
                if (newValue == 0 && oldValue > 0)
                {
                    _numberElementsZero[column]++;
                }
                else if (newValue != 0 && oldValue == 0)
                {
                    ulong numZeroElements = --_numberElementsZero[column];
                    if (numZeroElements < _rowZerosMin && (_columnAgingTasks[column] == null))
                    {
                        lock (_columnAgingTasks)
                        {
                            if (_columnAgingTasks[column] == null)
                            {
                                _columnAgingTasks[column] = Task.Run(() => AgeColumn(column));
                            }
                        }
                    }
                }
            }
        }

        protected void AgeColumn(long column)
        {
            long index = _agingIndex[column];
            while (_numberElementsZero[column] < _rowZerosMax)
            {
                ulong value;
                while ((value = Read(column, index)) == 0)
                {
                    if (++index >= NumberOfRows)
                        index = 0;
                }
                Write(column, index, --value);
            }
            _agingIndex[column] = index;

            lock (_columnAgingTasks)
            {
                _columnAgingTasks[column] = null;
            }
        }

    }

    public class AgingMembershipSketch : AgingSketch
    {
        public AgingMembershipSketch(long numberOfColumns, long numberOfRows, int bitsPerElement = 2, float fractionRowZerosMin = .45f, float fractionRowZerosMax = .55f)
            : base(numberOfColumns, numberOfRows, bitsPerElement, fractionRowZerosMin, fractionRowZerosMax)
        {
        }

        public bool AddMember(string s)
        {
            return Add(s, 2).PriorMin > 0;
        }

        public bool IsMember(string s)
        {
            return IsNonZero(s);
        }

    }
}
