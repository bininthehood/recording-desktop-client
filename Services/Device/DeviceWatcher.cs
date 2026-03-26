using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Diagnostics;
using DeviceState = NAudio.CoreAudioApi.DeviceState;
using Role = NAudio.CoreAudioApi.Role;

namespace RecordClient.Services.Device
{
    public class DeviceChangedEventArgs : EventArgs
    {
        public string Reason { get; }

        public DeviceChangedEventArgs(string reason)
        {
            Reason = reason;
        }
    }


    public class DeviceWatcher : IDisposable
    {
        private readonly DeviceWatcher _parent;


        private readonly MMDeviceEnumerator _enumerator;
        private readonly NotificationClient _callback;

        public event EventHandler<DeviceChangedEventArgs>? DeviceChanged;

        public DeviceWatcher()
        {
            _enumerator = new MMDeviceEnumerator();
            _callback = new NotificationClient(this);
            _enumerator.RegisterEndpointNotificationCallback(_callback);
#if DEBUG
            Debug.WriteLine($"[DeviceWatcher] 장치 감시 시작됨");
#endif
        }

        public void Dispose()
        {
            _enumerator.UnregisterEndpointNotificationCallback(_callback);
            _enumerator.Dispose();

#if DEBUG
            Debug.WriteLine("[DeviceWatcher] 장치 감시 종료됨");
#endif
        }

        private void RaiseDeviceChanged(string reason)
        {
            DeviceChanged?.Invoke(this, new DeviceChangedEventArgs(reason));
        }

        private class NotificationClient : IMMNotificationClient
        {

            private DateTime _lastNotified = DateTime.MinValue;
            private readonly TimeSpan _throttle = TimeSpan.FromMilliseconds(50);

            private void ThrottledLog(string message)
            {
                var now = DateTime.Now;
                if ((now - _lastNotified) > _throttle)
                {
                    _lastNotified = now;
                    Debug.WriteLine(message);
                }
            }
            private readonly DeviceWatcher _parent;
            public NotificationClient(DeviceWatcher parent)
            {
                _parent = parent;
            }

            public void OnDeviceAdded(string deviceId)
            {
#if DEBUG
                ThrottledLog($"[DeviceWatcher] 장치 추가됨: {deviceId}");
#endif
                _parent.RaiseDeviceChanged("Added");
            }

            public void OnDeviceRemoved(string deviceId)
            {
#if DEBUG
                ThrottledLog($"[DeviceWatcher] 장치 제거됨: {deviceId}");
#endif
                _parent.RaiseDeviceChanged("Removed");
            }

            public void OnDeviceStateChanged(string deviceId, DeviceState newState)
            {
#if DEBUG
                ThrottledLog($"[DeviceWatcher] 장치 상태 변경됨: {deviceId} → {newState}");
#endif
                _parent.RaiseDeviceChanged("State");
            }

            public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
            {
                string type = flow == DataFlow.Capture ? "마이크" : flow == DataFlow.Render ? "스피커" : "기타";

#if DEBUG
                ThrottledLog($"[DeviceWatcher] 기본 {type} 장치 변경됨: {defaultDeviceId}");
#endif
                _parent.RaiseDeviceChanged("Default");
            }

            public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
            {

#if DEBUG
                ThrottledLog($"[DeviceWatcher] 장치 속성 변경됨: {pwstrDeviceId}");
#endif
                _parent.RaiseDeviceChanged("Property");
            }
        }
    }

}
