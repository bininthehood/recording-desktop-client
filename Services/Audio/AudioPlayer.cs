using NAudio.Wave;
using RecordClient.Helpers;
using System.IO;
using System.Net.Http;
using static RecordClient.Helpers.InterfaceSocket.InterfaceSocket;

public class AudioPlayer
{
    private static readonly string TAG = typeof(AudioPlayer).Name;

    // 다운로드 관련 필드
    private static readonly HttpClient httpClient = new();

    private static WaveOutEvent? _waveOut;
    private static AudioFileReader? _reader;
    private static float _playbackSpeed = 1.0f;


    public string GetAudioStatus()
    {
        if (_waveOut == null)
            return "stop";

        return _waveOut.PlaybackState switch
        {
            PlaybackState.Playing => "play",
            PlaybackState.Paused => "pause",
            PlaybackState.Stopped => "stop",
            _ => "stop"
        };
    }

    public int SetAudioUpload(string gid, string id, string urlTemplate, List<string>? filenames)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gid) || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(urlTemplate))
            {
                Logger.Error("필수 파라미터(gid, id, url)가 누락되었습니다.");
                return ResultCode.BadRequest;
            }
            if (filenames == null || filenames.Count == 0)
            {
                Logger.Error("다운로드할 파일이 없습니다.");
                return ResultCode.BadRequest;
            }

            string voice = "voice";
            string multiyn = "multi";
            string saveFolder = Path.Combine("C:", "ITRAY", "tts", gid, id);
            Directory.CreateDirectory(saveFolder);

            bool allSuccess = true;

            foreach (var filename in filenames)
            {
                string url = urlTemplate
                    .Replace("{voice}", voice)
                    .Replace("{multiyn}", multiyn)
                    .Replace("{id}", id)
                    .Replace("{filename}", filename);
                string localPath = Path.Combine(saveFolder, filename);

                try
                {
                    var bytes = httpClient.GetByteArrayAsync(url).Result; // 동기 호출
                    File.WriteAllBytes(localPath, bytes);
                    Logger.Info($"다운로드 완료: {filename}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"다운로드 실패: {filename} - {ex.Message}");
                    allSuccess = false;
                }
            }

            return allSuccess ? ResultCode.Success : ResultCode.PartialSuccess;
        }
        catch (Exception ex)
        {

            Logger.Error($"다운로드 처리 중 오류 발생: {ex}");
            return ResultCode.InternalError;
        }
    }

    public int SetAudioStart(string gid, string id, string url, List<string>? filenames)
    {
        string folderPath = Path.Combine("C:", "ITRAY", "tts", gid, id);

        if (!Directory.Exists(folderPath))
        {
            Logger.Error($"[{TAG}] 경로 없음 .. 다운로드 중 : {folderPath}");

            SetAudioUpload(gid, id, url, filenames);
        }
        if (filenames == null || filenames.Count == 0)
        {
            Logger.Error($"[{TAG}] 재생할 MP3 파일이 없습니다.");
            return ResultCode.BadRequest;
        }

        try
        {
            foreach (var file in filenames)
            {
                Logger.Info($"[AudioPlayer] 재생 시작: {Path.GetFileName(file)}");

                _reader = new AudioFileReader(file);
                _reader.Volume = 1.0f;

                _waveOut = new WaveOutEvent();
                _waveOut.Init(_reader);
                _waveOut.Play();

                while (_waveOut.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(100);
                }

                _waveOut.Dispose();
                _reader.Dispose();
                _waveOut = null;
                _reader = null;

                Logger.Info($"[AudioPlayer] 재생 완료: {Path.GetFileName(file)}");
            }

            return ResultCode.Success;
        }
        catch (Exception ex)
        {
            Logger.Error("[AudioPlayer] 재생 중 오류 발생: " + ex.Message);
            return ResultCode.InternalError;
        }
    }

    public int SetAudioPause()
    {
        if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
        {
            _waveOut.Pause();
            Logger.Info("[AudioPlayer] 재생 일시정지");
            return ResultCode.Success;
        }

        Logger.Error("[AudioPlayer] 재생 중이 아님");
        return ResultCode.BadRequest;
    }

    public int SetAudioRestart()
    {
        if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Paused)
        {
            _waveOut.Play();
            Logger.Info("[AudioPlayer] 재생 재개");
            return ResultCode.Success;
        }

        Logger.Error("[AudioPlayer] 일시정지 상태 아님");
        return ResultCode.BadRequest;
    }

    public int SetAudioStop()
    {
        try
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _reader?.Dispose();
            _waveOut = null;
            _reader = null;

            Logger.Info("[AudioPlayer] 오디오 재생 중지 및 자원 해제 완료");
            return ResultCode.Success;
        }
        catch (Exception ex)
        {
            Logger.Error("[AudioPlayer] 중지 오류: " + ex.Message);
            return ResultCode.InternalError;
        }
    }

    public int SetAudioSpeed(float speed)
    {
        if (speed <= 0.1f || speed > 3.0f)
        {
            Logger.Error("[AudioPlayer] 재생 속도 허용 범위 초과");
            return ResultCode.BadRequest;
        }

        Logger.Error("[AudioPlayer] NAudio 기본 구조로는 속도 변경 미지원 - 커스텀 구현 필요");
        return ResultCode.NotImplemented;
    }

}
