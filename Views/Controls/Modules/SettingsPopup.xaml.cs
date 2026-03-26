using RecordClient.Helpers.Config;
using RecordClient.Helpers.Popup;
using RecordClient.Services.Login;
using RecordClient.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Windows;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using UserControl = System.Windows.Controls.UserControl;

namespace RecordClient.Views.Controls.Modules
{
    /// <summary>
    /// SettingsPopup.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SettingsPopup : UserControl
    {

        // .ini 설정 파일 필드
        private IniFile? _ini;
        private bool isWeb_prev;
        public SettingsPopup()
        {
            InitializeComponent();
        }

        private void SettingPage_Loaded(object sender, RoutedEventArgs e)
        {

        }

        public void LoadServerSettings()
        {
            _ini = new IniFile(Path.Combine(AppContext.BaseDirectory, "config.ini"));
            // ini 파일 로딩
            if (_ini != null)
            {
                try
                {
                    ServerIpTextBox.Text = _ini.Get("Server", "Host");
                    ServerPortTextBox.Text = _ini.Get("Server", "Port");
                    ServerPathTextBox.Text = _ini.Get("Server", "Path");
                    isWeb_prev = _ini.Get("Client", "Web") == "true" ? true : false;
                    ClientModeWeb.IsChecked = isWeb_prev;

                    EnableTextBox();
                }
                catch (Exception ex)
                {
                    Alert.Show("ERROR", $"설정 파일 로딩 실패: {ex.Message}");
                }

            }
            else
            {
                Alert.Show("ERROR", "설정 정보가 누락되었습니다 ..  ");
            }
        }
        private async void SaveServerSettings_Click(object sender, RoutedEventArgs e)
        {
            //if (MainWindow.DataContext is MainPageViewModel viewModel)
            //{

                if (_ini == null) return;
            string ip = ServerIpTextBox.Text.Trim();
            string port = ServerPortTextBox.Text.Trim();
            string path = ServerPathTextBox.Text.Trim();
            //string isWeb = "false";

            bool? isWeb = ClientModeWeb.IsChecked;
            string _isWeb = isWeb == true ? "true" : "false";

            if (string.IsNullOrWhiteSpace(ip) || !int.TryParse(port, out _))
            {
                Alert.Show("ERROR", "올바른 서버 정보가 아닙니다.");
                return;
            }

            try
            {
                bool reConnect = await LoginService.LinkToServerAsync(new TcpClient(), ip, int.Parse(port));

                if (reConnect)
                {
                    _ini.Set("Server", "Host", ip);
                    _ini.Set("Server", "Port", port);
                    _ini.Set("Server", "Path", path);

                    if (isWeb_prev != ClientModeWeb.IsChecked)
                    {
                        if (Window.GetWindow(this) is MainWindow mainWindow)
                        {
                            bool shouldExit = Confirm.Show("클라이언트의 연동 정보를 변경 하시겠습니까? \n프로그램이 재실행 됩니다.");
                            if (shouldExit)
                            {
                                _ini.Set("Client", "Web", _isWeb);
                                _ini.Save();

                                try
                                {
                                    string exePath = Process.GetCurrentProcess().MainModule.FileName;

                                    if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                                    {
                                        Alert.Show("ERROR", "앱 경로를 찾을 수 없어 재실행할 수 없습니다.");
                                        return;
                                    }

                                    Process.Start(exePath);  // 앱 재시작
                                    Application.Current.Shutdown();  // 기존 앱 종료
                                }
                                catch (Exception ex)
                                {
                                    Alert.Show("ERROR", $"앱 재실행 중 오류 발생: {ex.Message}");
                                }


                            }
                        }

                    }
                    _ini.Save();

                    Alert.Show("INFO", "서버 설정이 저장되었습니다.");
                }
                else
                {
                    Alert.Show("ERROR", "서버 연결에 실패하였습니다.");
                }
            }
            catch (Exception ex)
            {
                Alert.Show("ERROR", $"서버 연결에 실패하였습니다. {ex.Message}");
                throw;
            }

            hideSettingsPopup();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            hideSettingsPopup();
        }

        private void hideSettingsPopup()
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.HideSettingsPopup();
        }

        private void ClientMode_On(object sender, RoutedEventArgs e)
        {
            ClientModeWeb.Content = "사용";
            ClientModeWeb.Background = Brushes.DeepPink;
            EnableTextBox();
        }

        private void ClientMode_Off(object sender, RoutedEventArgs e)
        {
            ClientModeWeb.Content = "미사용";
            ClientModeWeb.Background = Brushes.Gray;
            EnableTextBox();
        }

        private void EnableTextBox()
        {
            // UI제어 : 웹 연동 시 host / post 설정 불가 ( 웹에서 전달받는 값으로 저장됨 ) 
            if (ClientModeWeb.IsChecked == true)
            {
                ServerIpTextBox.IsEnabled = false;
                ServerIpTextBox.Cursor = Cursors.No;
                ServerIpTextBox.CaretBrush = Brushes.Transparent;
                ServerIpTextBox.Opacity = 0.5;

                ServerPortTextBox.IsEnabled = false;
                ServerPortTextBox.Cursor = Cursors.No;
                ServerPortTextBox.CaretBrush = Brushes.Transparent;
                ServerPortTextBox.Opacity = 0.5;
            }
            else
            {
                ServerIpTextBox.IsEnabled = true;
                ServerIpTextBox.Cursor = Cursors.IBeam;
                ServerIpTextBox.CaretBrush = Brushes.Black;
                ServerIpTextBox.Opacity = 1;

                ServerPortTextBox.IsEnabled = true;
                ServerPortTextBox.Cursor = Cursors.IBeam;
                ServerPortTextBox.CaretBrush = Brushes.Black;
                ServerPortTextBox.Opacity = 1;
            }
        }
    }
}
