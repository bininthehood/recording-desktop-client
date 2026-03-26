using System.Runtime.InteropServices;

/// <summary> 
/// COM 객체: PolicyConfigClient
/// - 실제 COM 객체를 생성할 때 사용됨 (CLSID)
/// - SetDefaultEndpoint를 포함한 오디오 정책 제어 기능을 제공
/// </summary>
[ComImport]
[Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
class PolicyConfigClient
{
}


/// <summary>
/// COM 인터페이스: IPolicyConfig
/// - 오디오 장치의 설정을 제어하기 위한 인터페이스
/// - 기본 오디오 장치 설정, 공유 모드 설정, 프로퍼티 제어 등 제공
/// </summary>
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
interface IPolicyConfig
{
    int GetMixFormat(string pszDeviceName, out IntPtr ppFormat);
    int GetDeviceFormat(string pszDeviceName, bool bDefault, out IntPtr ppFormat);
    int ResetDeviceFormat(string pszDeviceName);
    int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
    int GetProcessingPeriod(string pszDeviceName, bool bDefault, out long pmftDefault, out long pmftMinimum);
    int SetProcessingPeriod(string pszDeviceName, ref long pmftPeriod);
    int GetShareMode(string pszDeviceName, out IntPtr pMode);
    int SetShareMode(string pszDeviceName, IntPtr mode);

    // 디바이스 속성 읽기/쓰기
    int GetPropertyValue(string pszDeviceName, ref PROPERTYKEY key, out PROPVARIANT pv);
    int SetPropertyValue(string pszDeviceName, ref PROPERTYKEY key, ref PROPVARIANT pv);

    // 기본 오디오 장치 설정 메서드
    int SetDefaultEndpoint(
        [MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId,
        ERole eRole
    );

    int SetEndpointVisibility(string pszDeviceName, bool bVisible);
}


/// <summary>
/// 구조체: PROPERTYKEY
/// - 윈도우 프로퍼티 시스템의 키 형식 (fmtid + pid 조합)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PROPERTYKEY
{
    public Guid fmtid;
    public int pid;
}

/// <summary>
/// 구조체: PROPVARIANT
/// - 프로퍼티 값 전달용 구조체 (Variant 형태, 일부 필드만 정의됨)
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct PROPVARIANT
{
    [FieldOffset(0)]
    public ushort vt;            // Variant 타입 (예: VT_LPWSTR 등)
    [FieldOffset(8)]
    public IntPtr pointerValue;  // 포인터 값 (문자열 등)
}


/// <summary>
/// 열거형: ERole
/// - 기본 오디오 장치 설정 시 사용하는 역할 정의
/// </summary>
public enum ERole
{
    eConsole = 0,        // 시스템 기본 (일반 시스템 사운드)
    eMultimedia = 1,     // 음악, 영화 등 멀티미디어
    eCommunications = 2  // 화상회의, 통화 등 커뮤니케이션
}
