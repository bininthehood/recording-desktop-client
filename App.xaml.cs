using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using RecordClient.Helpers;
using RecordClient.Helpers.Config;
using RecordClient.Helpers.InterfaceSocket;
using RecordClient.Helpers.Popup;
using RecordClient.ViewModels;
using RecordClient.Views;
using RecordClient.Views.Controls;
using System.IO;
using System.Windows;

namespace RecordClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            Logger.Init(); // 로깅 초기화

            const string mutexName = "VoiceLog_Unique_Mutex";
            const string shutdownEventName = "VoiceLog_Shutdown_Event";
            bool createdNew;

            _mutex = new Mutex(true, $"Global\\{mutexName}", out createdNew);

            if (!createdNew)
            {
                try
                {
                    using var shutdownEvent = EventWaitHandle.OpenExisting(shutdownEventName);
                    shutdownEvent.Set(); // 실행 중인 인스턴스에 종료 신호 전송
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    MessageBox.Show("이미 실행 중인 프로그램을 종료할 수 없습니다.");
                }

                App.Current.Shutdown();
                return;
            }

            try
            {
                // 전역 종료 이벤트 핸들 생성
                var shutdownWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, shutdownEventName);

                // 안전한 백그라운드 스레드 방식 (ExecutionEngineException 방지)
                Thread shutdownThread = new(() =>
                {
                    try
                    {
                        shutdownWaitHandle.WaitOne();

                        // Dispatcher가 살아 있을 때만 안전하게 호출
                        if (Application.Current?.Dispatcher != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Logger.Info("[App] 외부 종료 요청 수신 → 앱 종료");
                                Application.Current.Shutdown();
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        File.WriteAllText("crash.log", $"[ShutdownThread Error] {ex}");
                    }
                });
                shutdownThread.IsBackground = true;
                shutdownThread.Start();

                // 전역 예외 처리기 등록
                AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
                {
                    File.WriteAllText("crash.log", $"[UnhandledException] {ex.ExceptionObject}");
                };

                DispatcherUnhandledException += (s, ex) =>
                {
                    File.WriteAllText("crash.log", $"[DispatcherException] {ex.Exception.Message}\n{ex.Exception.StackTrace}");
                    ex.Handled = true;
                };

                // 초기 설정
                string configPath = Path.Combine(AppContext.BaseDirectory, "config.ini");
                var ini = new IniFile(configPath);

                // 시작 프로그램 등록
                RegisterStartup(true);

                base.OnStartup(e); // 반드시 마지막에 호출
            }
            catch (Exception ex)
            {
                MessageBox.Show($"시작 오류: {ex.Message}");
            }
        }


        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex = null;

            if (Application.Current.MainWindow is MainWindow window &&
                 window.MainContentHost.Content is MainPage mainPage)
            {
                mainPage.DisposeViewResources();
            }

            InterfaceSocket.Instance.Stop();
            InterfaceSocket.Instance._isRunning = false;

            base.OnExit(e);
        }
        private void TrayMenu_Open_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow;
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        }

        public static void RegisterStartup(bool enable)
        {
            string appName = "VoiceLog";
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

            Logger.Info($"시작 프로그램 등록 .. {exePath}");

            RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

            if (enable)
                rk.SetValue(appName, $"\"{exePath}\"");
            else
                rk.DeleteValue(appName, false);
        }


        private void TrayMenu_Exit_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.DataContext is MainPageViewModel viewModel)
            {

                bool shouldExit = Confirm.Show("녹취가 진행 중입니다. 프로그램을 종료하시겠습니까?");
                if (shouldExit)
                {
                    // 리소스 해제 후 종료
                    try
                    {
                        viewModel.StopRecording();         // 자원 해제
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"종료 중 예외 발생: {ex.Message}");
                    }

                    App.Current.Shutdown();
                }
            }
            else
            {
                bool shouldExit = Confirm.Show("프로그램을 종료하시겠습니까?");
                if (shouldExit)
                {
                    App.Current.Shutdown();

                    (Application.Current.FindResource("TrayIcon") as TaskbarIcon)?.Dispose();
                }
            }
        }
    }

}
