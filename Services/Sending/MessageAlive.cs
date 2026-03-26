using System.Text;

namespace RecordClient.Services.Sending
{

    public class MessageAlive : Message
    {
        public override byte[] GetByte()
        {
            // 현재 날짜 및 시간 포맷 설정 (yyyyMMddHHmmss)
            string nowDatetime = DateTime.Now.ToString("yyyyMMddHHmmss");

            string nowDate = nowDatetime.Substring(0, 8);
            string nowTime = nowDatetime.Substring(8, 6);

            // Header Data 생성
            StringBuilder headerData = new StringBuilder();
            headerData.Append("04"); // 메시지 타입
            headerData.Append("200"); // 상태 코드
            headerData.Append(string.Format("{0,30:D}", 0)); // 예약된 30자리 숫자
            headerData.Append(string.Format("{0:D6}", 0)); // 예약된 6자리 숫자

            // Body Data
            string bodyData = "";

            // --------------------------------------------------
            // Send Format Parsing
            // --------------------------------------------------
            byte[] bodyDataByte = Encoding.UTF8.GetBytes(bodyData);
            int bodySize = bodyDataByte.Length;

            headerData.Append(string.Format("{0:D6}", bodySize)); // 바디 데이터 크기 추가

            byte[] headerDataByte = Encoding.UTF8.GetBytes(headerData.ToString());
            int headerSize = headerDataByte.Length;

            // Set Buffer
            byte[] buffer = new byte[headerSize + bodySize];
            Buffer.BlockCopy(headerDataByte, 0, buffer, 0, headerSize);
            Buffer.BlockCopy(bodyDataByte, 0, buffer, headerSize, bodySize);

            return buffer;
        }
    }

}
