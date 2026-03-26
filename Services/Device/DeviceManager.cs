using NAudio.CoreAudioApi;
using RecordClient.Helpers;
using RecordClient.Models.Device;

namespace RecordClient.Services
{
    public static class DeviceManager
    {
        private static readonly string TAG = nameof(DeviceManager);
        private static string? _lastSetDeviceId;

        private static IPolicyConfig CreatePolicyConfig()
        {
            try
            {
                var obj = new PolicyConfigClient() as IPolicyConfig;
                if (obj == null) throw new Exception("COM 생성 실패");
                return obj;
            }
            catch (Exception ex)
            {
                Logger.Error("PolicyConfigClient 생성 실패: " + ex);
                Environment.FailFast("COM 객체 생성 실패", ex);
                throw;
            }
        }

        public static Task SetDeviceAsync(DeviceItem deviceItem, DataFlow dataFlow)
        {
            var tcs = new TaskCompletionSource();

            Thread thread = new(() =>
            {
                try
                {
                    if (deviceItem?.ID == null)
                    {
                        Logger.Warn("DeviceItem 또는 ID가 null임");
                        tcs.TrySetResult(); return;
                    }

                    using var enumerator = new MMDeviceEnumerator();
                    var devices = enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active);
                    var targetDevice = devices.FirstOrDefault(d => d.ID == deviceItem.ID);
                    if (targetDevice == null)
                    {
                        Logger.Warn("해당 ID의 장치를 찾을 수 없음");
                        tcs.TrySetResult(); return;
                    }

                    string id = targetDevice.ID;

                    if (_lastSetDeviceId == id)
                    {
                        Logger.Debug("동일한 장치 설정 요청 - 무시");
                        tcs.TrySetResult(); return;
                    }

                    // COM 객체 생성
                    if (CreatePolicyConfig() is not IPolicyConfig policyConfig)
                    {
                        Logger.Error("PolicyConfig COM 객체 생성 실패");
                        tcs.TrySetResult(); return;
                    }

                    // 장치 설정
                    policyConfig.SetDefaultEndpoint(id, ERole.eConsole);
                    policyConfig.SetDefaultEndpoint(id, ERole.eMultimedia);
                    policyConfig.SetDefaultEndpoint(id, ERole.eCommunications);

                    _lastSetDeviceId = id;

                    tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    Logger.Error("SetDeviceAsync 오류: " + ex);
                    tcs.TrySetException(ex);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            return tcs.Task;
        }


    }

}
