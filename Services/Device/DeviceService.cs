using NAudio.CoreAudioApi;
using RecordClient.Helpers;
using RecordClient.Helpers.Popup;
using RecordClient.Models.Device;
using RecordClient.Services.Record;

namespace RecordClient.Services
{
    public class DeviceService
    {
        public static DeviceService Instance { get; } = new DeviceService();
        private DeviceService() { } // 외부에서 new 방지

        private readonly VoiceManager voiceManager = VoiceManager.GetInstance();

        public void CommitInputVolume(float volume)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                if (device != null && device.State == DeviceState.Active)
                {
                    device.AudioEndpointVolume.MasterVolumeLevelScalar = volume;
                    voiceManager.PushCaptureVol(200, (int)(volume * 100));

                    Alert.Show("INFO", $"입력 볼륨: {(int)Math.Floor(volume * 100)}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"입력 볼륨 설정 실패: {ex.Message}");
            }
        }


        public void CommitOutputVolume(float volume)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                if (device != null && device.State == DeviceState.Active)
                {
                    device.AudioEndpointVolume.MasterVolumeLevelScalar = volume;
                    voiceManager.PushRenderVol(200, (int)(volume * 100));

                    Alert.Show("INFO", $"출력 볼륨: {(int)Math.Floor(volume * 100)}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"출력 볼륨 설정 실패: {ex.Message}");
            }
        }

        public void CommitInputMute(bool mute)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                if (device != null)
                {
                    device.AudioEndpointVolume.Mute = mute;

                    string r = mute ? "" : "해제";
                    Alert.Show("INFO", $"마이크 음소거 {r} ");
                    voiceManager.PushCaptureMute(200, mute);
                }


            }
            catch (Exception ex)
            {
                Logger.Error($"입력 볼륨 음소거 실패: {ex.Message}");
            }
        }

        public void CommitOutputMute(bool mute)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (device != null)
                    device.AudioEndpointVolume.Mute = mute;

                string r = mute ? "" : "해제";
                Alert.Show("INFO", $"스피커 음소거 {r}");
                voiceManager.PushRenderMute(200, mute);
            }
            catch (Exception ex)
            {
                Logger.Error($"출력 볼륨 음소거 실패: {ex.Message}");
            }
        }

        public float GetInputVolume()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                DeviceItem deviceItem = new DeviceItem
                {
                    Name = device.FriendlyName,
                    ID = device.ID
                };

                return device?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0;
            }
            catch (Exception ex)
            {
                Logger.Error($"입력 볼륨 가져오기 실패: {ex.Message}");
                return 0;
            }

        }

        public float GetOutputVolume()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                DeviceItem deviceItem = new DeviceItem
                {
                    Name = device.FriendlyName,
                    ID = device.ID
                };

                return device?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0;

            }
            catch (Exception ex)
            {
                Logger.Error($"출력 볼륨 가져오기 실패: {ex.Message}");
                return 0;
            }
        }

        public bool GetInputMute()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                var deviceItem = new DeviceItem
                {
                    Name = device.FriendlyName,
                    ID = device.ID
                };

                return device.AudioEndpointVolume.Mute;
            }
            catch (Exception ex)
            {
                Logger.Error($"입력 음소거 여부 가져오기 실패: {ex.Message}");
                return false;
            }
        }
        public bool GetOutputMute()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var deviceItem = new DeviceItem
                {
                    Name = device.FriendlyName,
                    ID = device.ID
                };

                return device.AudioEndpointVolume.Mute;
            }
            catch (Exception ex)
            {
                Logger.Error($"출력 음소거 여부 가져오기 실패: {ex.Message}");
                return false;
            }
        }

        public bool IsInputDeviceAvailable()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                return defaultDevice != null && defaultDevice.State == DeviceState.Active;
            }
            catch (Exception ex)
            {
                Logger.Error($"[DeviceService] 입력 장치 검증 실패: {ex.Message}");
                return false;
            }
        }

        public List<DeviceItem> GetInputDevices()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                                  .Select(d => new DeviceItem
                                  {
                                      Name = d.FriendlyName,
                                      ID = d.ID
                                  })
                                  .ToList();
            }
            catch (Exception ex)
            {
                Logger.Error($"[DeviceService] 입력 장치 목록 가져오기 실패: {ex.Message}");
                return [];
            }
        }

        public List<DeviceItem> GetOutputDevices()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                                  .Select(d => new DeviceItem
                                  {
                                      Name = d.FriendlyName,
                                      ID = d.ID
                                  })
                                  .ToList();
            }
            catch (Exception ex)
            {
                Logger.Error($"[DeviceService] 출력 장치 목록 가져오기 실패: {ex.Message}");
                return [];
            }
        }

        public List<string> GetInputDeviceListString()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                List<string> devicesNames = new List<string>();

                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
                foreach (var device in devices)
                {
                    string deviceName = device.FriendlyName;
                    devicesNames.Add(deviceName);
                }

                return devicesNames;
            }
            catch (Exception ex)
            {
                Logger.Error($"[DeviceService] 입력 장치 목록 가져오기 실패: {ex.Message}");
                return [];
            }
        }
        public List<string> GetOutputDeviceListString()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                List<string> devicesNames = new List<string>();

                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
                foreach (var device in devices)
                {
                    string deviceName = device.FriendlyName;
                    devicesNames.Add(deviceName);
                }

                return devicesNames;
            }
            catch (Exception ex)
            {
                Logger.Error($"[DeviceService] 출력 장치 목록 가져오기 실패: {ex.Message}");
                return [];
            }
        }

        public MMDevice? GetOutputDeviceById(string id)
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                                 .FirstOrDefault(d => d.ID == id);
            }
            catch (Exception ex)
            {
                Logger.Error($"[DeviceService] ID: 출력 장치 가져오기 실패: {ex.Message}");
                return null;
            }
        }
        public MMDevice? GetInputDeviceById(string id)
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                                 .FirstOrDefault(d => d.ID == id);

            }
            catch (Exception ex)
            {
                Logger.Error($"[DeviceService] ID: 입력 장치 가져오기 실패: {ex.Message}");
                return null;
            }
        }
        public MMDevice? GetOutputDeviceByName(string name)
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                                 .FirstOrDefault(d => d.ID == name);
            }
            catch (Exception ex)
            {
                Logger.Error($"[DeviceService] Name: 출력 장치 가져오기 실패: {ex.Message}");
                return null;
            }
        }
        public MMDevice? GetInputDeviceByName(string name)
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                                 .FirstOrDefault(d => d.ID == name);

            }
            catch (Exception ex)
            {
                Logger.Error($"[DeviceService] Name: 입력 장치 가져오기 실패: {ex.Message}");
                return null;
            }
        }
        public string GetDefaultInputDeviceName()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);

                return device.FriendlyName;
            }
            catch (Exception ex)
            {
                Logger.Error($"[DeviceService] 기본 입력 장치명 가져오기 실패: {ex.Message}");
                return "";
            }
        }


        public string GetDefaultOutputDeviceName()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                return device.FriendlyName;
            }
            catch (Exception ex)
            {
                Logger.Error($"[DeviceService] 기본 출력 장치명 가져오기 실패: {ex.Message}");
                return "";
            }
        }
        public string? GetDefaultInputDeviceID()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                return device.ID;
            }
            catch (Exception ex)
            {
                Logger.Error($"[DeviceService] 기본 입력 장치 가져오기 실패: {ex.Message}");
                return null;
            }
        }

        public string? GetDefaultOutputDeviceID()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return device.ID;
            }
            catch (Exception ex)
            {
                Logger.Error($"[DeviceService] 기본 출력 장치 가져오기 실패: {ex.Message}");
                return null;
            }
        }


    }
}
