using RecordClient.Views;
using RecordClient.Views.Controls.Modules;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace RecordClient.Helpers.Popup
{
    public static class Alert
    {
        /// <summary>
        /// 전역 Toast 메시지 출력 (애니메이션 포함)
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        /// <param name="type">"INFO" 또는 "ERROR"</param>
        public static void Show(string type, string message)
        {
            UIHelper.RunView<MainWindow>(async (mainWindow) =>
            {

                // ToastContainer 찾기 (x:Name="ToastLayer")
                if (mainWindow.FindName("ToastLayer") is not ItemsControl host)
                {
                    Logger.Warn("ToastLayer가 MainWindow에 정의되어 있지 않습니다.");
                    return;
                }

                // 아이콘 경로 지정
                string iconPath = type == "ERROR"
                    ? "pack://application:,,,/Resources/Images/error_16x16.png"
                    : "pack://application:,,,/Resources/Images/check_16x16.png";

                ToastNotification? toast = null;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    toast = new ToastNotification
                    {
                        IconSource = new BitmapImage(new Uri(iconPath)),
                        Message = message,
                        Opacity = 0
                    };

                    // 최신 2개만 남기고 모두 제거
                    var toasts = host.Items.OfType<ToastNotification>().ToList();
                    if (toasts.Count >= 2)
                    {
                        var removeTargets = toasts.Take(toasts.Count - 1); // 마지막 1개는 유지
                        foreach (var oldToast in removeTargets)
                        {
                            host.Items.Remove(oldToast);
                        }
                    }

                    host.Items.Add(toast);

                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                    toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                });


                await Task.Delay(2000);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (toast != null)
                    {
                        var transform = new TranslateTransform();
                        toast.RenderTransform = transform;

                        var slideOut = new DoubleAnimation
                        {
                            From = 0,
                            To = 300,
                            Duration = TimeSpan.FromMilliseconds(350),
                            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                        };

                        var fadeOut = new DoubleAnimation
                        {
                            From = 1,
                            To = 0,
                            Duration = TimeSpan.FromMilliseconds(350),
                            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                        };

                        fadeOut.Completed += (s, e) => host.Items.Remove(toast);

                        transform.BeginAnimation(TranslateTransform.XProperty, slideOut);
                        toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                    }
                });

            });
        }
    }
}
