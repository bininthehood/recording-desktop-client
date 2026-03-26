using RecordClient.Services.Utils;
using System.Text;

namespace RecordClient.Services.Sending
{
    public class MessageCommentSend : Message
    {
        public string UserId { get; set; } = "";
        public string RecKey { get; set; } = "";
        public string SubKey { get; set; } = "";
        public string Comment { get; set; } = "";
        public override byte[] GetByte()
        {
            // 현재 날짜 및 시간 포맷 설정 (yyyyMMddHHmmss)
            string nowDatetime = DateTime.Now.ToString("yyyyMMddHHmmss");

            string nowDate = nowDatetime.Substring(0, 8);
            string nowTime = nowDatetime.Substring(8, 6);

            // Header Data 생성
            StringBuilder headerData = new StringBuilder();
            headerData.Append("99"); // 메시지 타입
            headerData.Append("200"); // 상태 코드
            headerData.Append(Util.SpacePadding("INSERT_COMMENT", 100)); // Key 100자리 공백 패딩
            headerData.Append(Util.ZeroPadding(0, 6)); // SubKey 6자리 0 패딩

            // Body Data 생성
            StringBuilder bodyData = new StringBuilder();
            bodyData.Append(Util.SpacePadding(UserId, 40)); // 유저 아이디 라인
            bodyData.Append(Util.SpacePadding(RecKey, 100)); // 녹취 키 라인
            bodyData.Append(Util.SpacePadding(SubKey, 10)); // 보조 키 라인
            bodyData.Append(Util.SpacePadding(Comment, 600)); // 코멘트 라인

            // Body Data를 바이트 배열로 변환
            byte[] bodyDataByte = Encoding.UTF8.GetBytes(bodyData.ToString());
            int bodySize = bodyDataByte.Length;

            // Header에 Body Size 추가
            headerData.Append(string.Format("{0:D6}", bodySize));

            byte[] headerDataByte = Encoding.UTF8.GetBytes(headerData.ToString());
            int headerSize = headerDataByte.Length;

            // 최종 바이트 배열 생성
            byte[] buffer = new byte[headerSize + bodySize];
            Buffer.BlockCopy(headerDataByte, 0, buffer, 0, headerSize);
            Buffer.BlockCopy(bodyDataByte, 0, buffer, headerSize, bodySize);

            return buffer;
        }
    }
}
