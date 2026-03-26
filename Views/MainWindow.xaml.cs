using Hardcodet.Wpf.TaskbarNotification;
using MahApps.Metro.Controls;
using RecordClient.Helpers;
using RecordClient.Helpers.Config;
using RecordClient.Helpers.Popup;
using RecordClient.Models.Login;
using RecordClient.ViewModels;
using RecordClient.Views.Controls;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

// 초기화 시에 불필요하게 로그인페이지 , 메인페이지에 할당하면서 초기화로직이 비 웹화면일때도 탄다. 
// 에러는 없지만 불필요한 접근
namespace RecordClient.Views
{
    public partial class MainWindow : MetroWindow
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        const int DWMWA_NCRENDERING_POLICY = 2;
        const int DWMNCRP_DISABLED = 1;

        public LoginInfo CurrentUserInfo = new();
        public static bool isWeb = false;

        // 페이지 관련 필드
        private LoginPage _loginPage = new LoginPage();
        private MainPage _mainPage;
        private MainPageViewModel? _viewModel;

        // 윈도우 트레이 필드
        private TaskbarIcon? _trayIcon;

        private readonly bool _isAutoStart;
        public MainWindow(bool isAutoStart)
        {
            try
            {
                InitializeComponent();
                InitializeTrayIcon();

                _isAutoStart = isAutoStart;

                if (_isAutoStart)
                    Loaded += (s, e) => HideToTray(); // 시스템 트레이만 실행

                DisableShadow();

                this.KeyDown += MainWindow_KeyDown;
            }
            catch (Exception ex)
            {
                File.WriteAllText("mainwindow_crash.log", $"[MainWindow 생성자 예외]\n{ex.Message}\n{ex.StackTrace}");
            }

        }

        internal void SetCurrentUserInfo(LoginInfo loginInfo)
        {
            CurrentUserInfo = loginInfo;
        }
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && PopupOverlay.Visibility == Visibility.Visible)
            {
                HideSettingsPopup();
            }
        }

        private void DisableShadow()
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int value = DWMNCRP_DISABLED;
                DwmSetWindowAttribute(hwnd, DWMWA_NCRENDERING_POLICY, ref value, Marshal.SizeOf(typeof(int)));
            }
            catch (Exception)
            {

            }
        }

        // 윈도우 닫기 시
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;

            HideToTray();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 윈도우 창 모서리 둥글게
            var rect = new RectangleGeometry
            {
                RadiusX = 0,
                RadiusY = 0,
                Rect = new Rect(0, 0, ActualWidth, ActualHeight)
            };
            Clip = rect;


            // 설정 파일에서 web연동 정보를 불러와서 초기화면을 분기한다.
            string iniPath = Path.Combine(AppContext.BaseDirectory, "config.ini");
            var _ini = new IniFile(iniPath);
            var clientSetting = _ini.Get("Client", "Web");
            if (!string.IsNullOrEmpty(clientSetting))
            {
                isWeb = clientSetting == "true" ? true : false;
                if (isWeb)
                {
                    await NavigateToMainPageAsync();
                }
                else
                {
                    MainContentHost.Content = _loginPage;
                    _loginPage.LoginBypass(); // 자동 로그인
                }
            }
            else
            {
                isWeb = true;
                await NavigateToMainPageAsync();

                //isWeb = false;
                //MainContentHost.Content = _loginPage;
            }

        }

        public async Task NavigateToMainPageAsync()
        {
            Logger.Info("MainPage 초기화 시작...");

            _viewModel = new MainPageViewModel();
            await _viewModel.InitializeAsync(); //  비동기 초기화

            var mainPage = new MainPage(_viewModel);
            MainContentHost.Content = mainPage;

            Logger.Info("MainPage 전환 완료");
        }



        // 윈도우 크기 조정 시 모서리 둥글게 유지
        private void MetroWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Clip is RectangleGeometry rect)
            {
                rect.Rect = new Rect(0, 0, ActualWidth, ActualHeight);
            }
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
                _trayIcon.Icon = new Icon(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources/Images/logo.ico"));
            }
            catch (Exception)
            {

            }
        }

        public void ShowSettingsPopup()
        {
            PopupOverlay.Visibility = Visibility.Visible;
            SettingsPopupControl.LoadServerSettings();
        }

        public void HideSettingsPopup()
        {
            PopupOverlay.Visibility = Visibility.Collapsed;
        }

        // 최소화 버튼 클릭 이벤트
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => Window.GetWindow(this).WindowState = WindowState.Minimized;

        // 닫기 버튼 클릭 이벤트
        private void CloseButton_Click(object sender, RoutedEventArgs e) => HideToTray();

        // 드래그로 창 이동
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                Window.GetWindow(this).DragMove();
            }
        }

        private void HideToTray()
        {
            this.Hide(); // 창 숨기기
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            var contextMenu = new ContextMenu();

            var settingsItem = new MenuItem { Header = "설정" };
            settingsItem.Click += MenuItem_Settings_Click;

            var logoutItem = new MenuItem { Header = "로그아웃" };
            logoutItem.Click += MenuItem_Logout_Click;

            var exitItem = new MenuItem { Header = "종료" };
            exitItem.Click += MenuItem_Exit_Click;

            if (MainContentHost.Content is MainPage mainPage)
            {
                contextMenu.Items.Add(settingsItem);

                if (!isWeb) contextMenu.Items.Add(logoutItem);

                contextMenu.Items.Add(new Separator());
            }
            contextMenu.Items.Add(exitItem);

            contextMenu.PlacementTarget = MenuButton;
            contextMenu.Placement = PlacementMode.Bottom;
            contextMenu.IsOpen = true;
        }
        private void MenuItem_Settings_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsPopup();
        }

        private void MenuItem_Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MainContentHost.Content is MainPage mainPage)
            {
                if (_viewModel != null)
                {
                    bool isRecording = _viewModel.IsRecording;

                    // 메인 페이지 상태 확인
                    string message = isRecording
                        ? "녹취가 진행 중입니다. 현재 녹취를 종료하고 로그아웃 하시겠습니까?"
                        : "로그아웃 하시겠습니까?";

                    if (!Confirm.Show(message)) return;

                    // 리소스 해제
                    try
                    {
                        mainPage.DisposeViewResources();

                        _viewModel = null;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"종료 중 예외 발생: {ex.Message}");
                    }

                    // 로그인 화면으로 전환
                    MainContentHost.Content = _loginPage;
                }
            }
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            string message = "";
            if (MainContentHost.Content is MainPage mainPage)
            {
                if (_viewModel != null)
                {
                    if (_viewModel.IsRecording)
                    {
                        message = "녹취가 진행 중입니다. 프로그램을 종료하시겠습니까?";
                    }
                    else
                    {
                        message = "프로그램을 종료하시겠습니까?";
                    }
                    bool shouldExit = Confirm.Show(message, this);
                    if (shouldExit)
                    {
                        // 리소스 해제 후 종료
                        try
                        {
                            mainPage.DisposeViewResources();

                            _viewModel = null;
                            
                            App.Current.Shutdown();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"종료 중 예외 발생: {ex.Message}");
                        }
                    }
                }
            }
            else
            {
                message = "프로그램을 종료하시겠습니까?";
                bool shouldExit = Confirm.Show(message, this);
                if (shouldExit)
                {
                    App.Current.Shutdown();
                }
            }
        }
    }
}
