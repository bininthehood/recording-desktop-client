namespace RecordClient.Services.Utils
{
    class AudioUtil
    {
        public static short[] ByteToShort(byte[] bytes)
        {
            if (bytes == null || bytes.Length % 2 != 0)
                throw new ArgumentException("입력된 바이트 배열이 null이거나 길이가 짝수가 아닙니다.");

            short[] shorts = new short[bytes.Length / 2];
            Buffer.BlockCopy(bytes, 0, shorts, 0, bytes.Length);
            return shorts;
        }


        public static byte[] ShortToByte(short[] shorts)
        {
            if (shorts == null)
            {
                throw new ArgumentNullException(nameof(shorts), "입력된 short 배열이 null입니다.");
            }

            byte[] bytes = new byte[shorts.Length * 2];

            try
            {
                Buffer.BlockCopy(shorts, 0, bytes, 0, bytes.Length);
            }
            catch (Exception)
            {
                bytes = new byte[shorts.Length * 2];
            }

            return bytes;
        }

    }
}
