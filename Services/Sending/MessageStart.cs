using RecordClient.Services.Utils;
using System.Text;

namespace RecordClient.Services.Sending
{
    public class MessageStart : Message
    {
        public string RecKey { get; set; } = "";
        public string SubKey { get; set; } = "";
        public int FileFormat { get; set; } = 1; // 1: wav, 2: mp3
        public int RecordType { get; set; } = 1; // 녹취유형
        public int EquipNo { get; set; } = 1; // 장비번호
        public int Channel { get; set; } = 1; // 채널 수

        public int SampleRate { get; set; } = 44100; // 샘플레이트
        public int BitPerSample { get; set; } = 16; // 비트퍼샘플
        public string UserId { get; set; } = ""; // 사용자 ID

        public override byte[] GetByte()
        {

            // 현재 날짜 및 시간 포맷 설정 (yyyyMMddHHmmss)
            string nowDatetime = DateTime.Now.ToString("yyyyMMddHHmmss");

            string nowDate = nowDatetime.Substring(0, 8);
            string nowTime = nowDatetime.Substring(8, 6);

            // Header Data 생성
            StringBuilder headerData = new StringBuilder();
            headerData.Append("01"); // 메시지 타입
            headerData.Append("200"); // 상태 코드
            headerData.Append(Util.SpacePadding(RecKey, 100)); // RecKey 100자리 공백 패딩
            headerData.Append(Util.ZeroPadding(int.Parse(SubKey), 6)); // SubKey 6자리 0 패딩

            // Body Data 생성
            StringBuilder bodyData = new StringBuilder();
            bodyData.Append(Util.ZeroPadding(FileFormat, 6)); // 파일 형식 패딩
            bodyData.Append(Util.ZeroPadding(RecordType, 6)); // 녹취 유형
            bodyData.Append(Util.ZeroPadding(EquipNo, 6)); // 장비 번호
            bodyData.Append(Util.ZeroPadding(Channel, 6)); // 채널 수
            bodyData.Append(Util.ZeroPadding(SampleRate, 6)); // 샘플레이트
            bodyData.Append(Util.ZeroPadding(BitPerSample, 6)); // 비트퍼샘플
            bodyData.Append(Util.SpacePadding(UserId, 50)); // 사용자 ID

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



