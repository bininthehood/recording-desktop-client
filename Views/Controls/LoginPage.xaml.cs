using RecordClient.Helpers;
using RecordClient.Helpers.Config;
using RecordClient.Helpers.Popup;
using RecordClient.Models.Login;
using RecordClient.Services.Login;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace RecordClient.Views.Controls
{
    /// <summary>
    /// LoginPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LoginPage : UserControl
    {
        public LoginPage()
        {
            InitializeComponent();
            Loaded += LoginPage_Loaded;
            PreviewKeyDown += Window_PreviewKeydown;


            var saved = LoginConfigService.Load();
            IniFile _ini = new IniFile(Path.Combine(AppContext.BaseDirectory, "config.ini"));
            string? ip = _ini.Get("Server", "Host");
            string? port = _ini.Get("Server", "Port");
            if (saved != null)
            {
                UserIdTextBox.Text = saved.UserId;
                PasswordBox.Password = saved.Password;
            }

            ServerIpTextBox.Text = ip;
            ServerPortTextBox.Text = port;
        }


        public void LoginBypass()
        {
            // 자동 로그인
            if (!string.IsNullOrEmpty(ServerIpTextBox.Text) && !string.IsNullOrEmpty(ServerPortTextBox.Text)
                && !string.IsNullOrEmpty(UserIdTextBox.Text) && !string.IsNullOrEmpty(PasswordBox.Password))
            {
                DispatcherTimer timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(0.5);
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    loginProcess();
                };
                timer.Start();
            }
        }
        private void LoginPage_Loaded(object sender, RoutedEventArgs e)
        {
        }
        private void Window_PreviewKeydown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(LoginButton, new RoutedEventArgs());
                e.Handled = true;
            }
        }
        private void LoginButton_Click(object sender, RoutedEventArgs e) // async 넣기
        {
            loginProcess();
        }
        private async void loginProcess()
        {
            LoadingOn();

            string serverIp = ServerIpTextBox.Text.Trim();
            string serverPort = ServerPortTextBox.Text.Trim();
            string userId = UserIdTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(serverIp) || string.IsNullOrEmpty(serverPort))
            {
                Alert.Show("ERROR", "서버 정보를 입력하세요");
                LoadingOff();
                return;
            }

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(password))
            {
                Alert.Show("ERROR", "아이디와 비밀번호를 입력하세요.");
                LoadingOff();
                return;
            }

            var result = await LoginService.Authenticate(serverIp, int.Parse(serverPort), userId, password);

            if (result)
            {

                IniFile _ini = new IniFile(Path.Combine(AppContext.BaseDirectory, "config.ini"));
                _ini.Set("Server", "Host", serverIp);
                _ini.Set("Server", "Port", serverPort);
                _ini.Save();

                LoginInfo loginInfo = new LoginInfo { UserId = userId, Password = password };
                LoginConfigService.Save(loginInfo);



                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.SetCurrentUserInfo(loginInfo);
                    await mainWindow.NavigateToMainPageAsync();

                }
                else
                {
                    Logger.Error("MainWindow 참조 실패 (Window.GetWindow(this) == null)");
                }

            }
            else
            {
                Alert.Show("ERROR", "로그인 실패. 서버 연결을 확인하세요.");
            }

            LoadingOff();
        }
        private void LoadingOn()
        {
            LoginButton.IsEnabled = false;
            LoginButtonText.Visibility = Visibility.Collapsed;
            LoginButtonRing.Visibility = Visibility.Visible;
            LoginButtonRing.IsActive = true;
        }

        private void LoadingOff()
        {
            LoginButtonRing.IsActive = false;
            LoginButtonRing.Visibility = Visibility.Collapsed;
            LoginButtonText.Visibility = Visibility.Visible;
            LoginButton.IsEnabled = true;
        }

        private void ServerIpTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void ServerPortTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }

}
