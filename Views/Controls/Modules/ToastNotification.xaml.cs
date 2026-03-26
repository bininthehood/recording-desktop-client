using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RecordClient.Views.Controls.Modules
{
    /// <summary>
    /// _ToastNotification.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ToastNotification : UserControl
    {
        public ToastNotification()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty IconSourceProperty =
            DependencyProperty.Register("IconSource", typeof(ImageSource), typeof(ToastNotification));
        public ImageSource IconSource
        {
            get => (ImageSource)GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }


        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(ToastNotification));
        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

    }

}
