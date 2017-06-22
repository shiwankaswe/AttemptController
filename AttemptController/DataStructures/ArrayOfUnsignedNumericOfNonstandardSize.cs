namespace AttemptController.DataStructures
{
    public class ArrayOfUnsignedNumericOfNonstandardSize
    {

        public byte[] AsByteArray { get; protected set; }

        public long LongLength { get; protected set; }
        public int Length => (int)LongLength;

        public int BitsPerElement { get; protected set; }

        protected ulong MaxValue;

        public ulong this[long index]
        {
            get { return ReadValue(index); }
            set { WriteValue(index, value); }
        }

        public static ArrayOfUnsignedNumericOfNonstandardSize Create( int bitsPerValue, long length)
        {
            return new ArrayOfUnsignedNumericOfNonstandardSize(bitsPerValue, length);
        }



        protected ArrayOfUnsignedNumericOfNonstandardSize(int bitsPerElement, long length)
        {
            LongLength = length;
            BitsPerElement = bitsPerElement;

            MaxValue = ((((ulong)1) << bitsPerElement) - 1);

            ulong lengthInBits = ((ulong)length * (ulong)bitsPerElement);
            ulong lengthInBytes = ( lengthInBits + 7) / 8;

            AsByteArray = new byte[lengthInBytes];
        }

        protected ulong ReadValue(long index)
        {
            ulong returnValue = 0;

            long leftmostBit = index * BitsPerElement;
            long byteIndexOfLeftmostBit = leftmostBit / 8;
            int bitIndexOfLeftmostBit = (int)(leftmostBit % 8);

            long rightmostBit = leftmostBit + BitsPerElement - 1;
            long byteIndexOfRightmostBit = rightmostBit / 8;
            int bitIndexOfRightmostBit = (int)(rightmostBit % 8);

            int bitsLeftAfterReadingFromThisByte = BitsPerElement;

            for (long byteIndex = byteIndexOfLeftmostBit; byteIndex <= byteIndexOfRightmostBit; byteIndex++)
            {
                int leftmostBitWithinByte = (byteIndex == byteIndexOfLeftmostBit) ? bitIndexOfLeftmostBit : 0;
                
                int rightmostBitWithinByte = (byteIndex == byteIndexOfRightmostBit) ? bitIndexOfRightmostBit : 7;

                int numberOfBitsToReadFromThisByte = rightmostBitWithinByte + 1 - leftmostBitWithinByte;
                
                bitsLeftAfterReadingFromThisByte -= numberOfBitsToReadFromThisByte;
                
                byte bitsFromArrayByte = AsByteArray[byteIndex];
                
                if (numberOfBitsToReadFromThisByte < 8)
                {
                    if (leftmostBitWithinByte > 0)
                    {
                        byte mask = (byte)(0xFF >> leftmostBitWithinByte); 
                        bitsFromArrayByte &= mask;
                    }
                    if (rightmostBitWithinByte < 7)
                    {
                        int shift = 7 - rightmostBitWithinByte;
                        bitsFromArrayByte >>= shift;
                    }
                }
                returnValue |= ((ulong)bitsFromArrayByte) << bitsLeftAfterReadingFromThisByte;
            }
            return returnValue;
        }



        protected void WriteValue(long index, ulong value)
        {
            if (value > MaxValue)
                value = MaxValue;

            long leftmostBit = index * BitsPerElement;
            long byteIndexOfLeftmostBit = leftmostBit / 8;
            int bitIndexOfLeftmostBit = (int)(leftmostBit % 8);

            long rightmostBit = leftmostBit + BitsPerElement - 1;
            long byteIndexOfRightmostBit = rightmostBit / 8;
            int bitIndexOfRightmostBit = (int)(rightmostBit % 8);

            int numberOfBitsLeftAfterThisWrite = BitsPerElement;

            for (long byteIndex = byteIndexOfLeftmostBit; byteIndex <= byteIndexOfRightmostBit; byteIndex++)
            {
                int leftmostBitWithinByte = byteIndex == byteIndexOfLeftmostBit ? bitIndexOfLeftmostBit : 0;

                int rightmostBitWithinByte = byteIndex == byteIndexOfRightmostBit ? bitIndexOfRightmostBit : 7;

                int numberOfBitsToWriteToThisByte = rightmostBitWithinByte + 1 - leftmostBitWithinByte;

                numberOfBitsLeftAfterThisWrite -= numberOfBitsToWriteToThisByte;

                byte valueToWrite = (byte)(value >> numberOfBitsLeftAfterThisWrite);

                if (numberOfBitsToWriteToThisByte == 8)
                {
                    AsByteArray[byteIndex] = valueToWrite;
                }
                else
                {
                    byte makeToRemoveBitsNotBeingWritten = 
                        (byte)((1 << numberOfBitsToWriteToThisByte) - 1);
                    valueToWrite &= makeToRemoveBitsNotBeingWritten;

                    byte maskOfLocationOfNewValue = 0xFF;
                    if (leftmostBitWithinByte > 0)
                    {
                        maskOfLocationOfNewValue &= (byte)(0xFF >> leftmostBitWithinByte);
                    }
                    if (rightmostBitWithinByte < 7)
                    {
                        int shift = 7 - rightmostBitWithinByte;
                        maskOfLocationOfNewValue &= (byte)(0xFF << shift);

                        valueToWrite <<= shift;
                    }

                    byte unmodifiedBitsFromOldValue = (byte)(AsByteArray[byteIndex] & (~maskOfLocationOfNewValue));

                    byte newValue = (byte)(unmodifiedBitsFromOldValue | valueToWrite);

                    AsByteArray[byteIndex] = newValue;
                }
            }
        }
    }


}
