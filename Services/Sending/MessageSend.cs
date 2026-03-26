using RecordClient.Services.Utils;
using System.Text;

namespace RecordClient.Services.Sending
{
    public class MessageSend : Message
    {
        public string RecKey { get; set; } = "";
        public string SubKey { get; set; } = "";
        public byte[] Data { get; set; } = Array.Empty<byte>();

        public override byte[] GetByte()
        {
            // 현재 날짜 및 시간 포맷 설정 (yyyyMMddHHmmss)
            string nowDatetime = DateTime.Now.ToString("yyyyMMddHHmmss");

            string nowDate = nowDatetime.Substring(0, 8);
            string nowTime = nowDatetime.Substring(8, 6);

            // Header Data 생성
            StringBuilder headerData = new StringBuilder();
            headerData.Append("03"); // 메시지 타입
            headerData.Append("200"); // 상태 코드
            headerData.Append(Util.SpacePadding(RecKey, 100)); // RecKey 100자리 공백 패딩
            headerData.Append(Util.ZeroPadding(int.Parse(SubKey), 6)); // SubKey 6자리 0 패딩

            // Body Data 처리
            int bodySize = Data.Length;
            headerData.Append(string.Format("{0:D6}", bodySize)); // 바디 데이터 크기 추가

            byte[] headerDataByte = Encoding.UTF8.GetBytes(headerData.ToString());
            int headerSize = headerDataByte.Length;

            // Set Buffer
            byte[] buffer = new byte[headerSize + bodySize];
            Buffer.BlockCopy(headerDataByte, 0, buffer, 0, headerSize);
            Buffer.BlockCopy(Data, 0, buffer, headerSize, bodySize);

            return buffer;
        }
    }
}
