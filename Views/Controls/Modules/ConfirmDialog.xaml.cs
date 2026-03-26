using MahApps.Metro.Controls;
using System.Windows;

namespace RecordClient.Views.Controls.Modules
{
    /// <summary>
    /// ConfirmDialog.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ConfirmDialog : MetroWindow
    {
        public bool Result { get; private set; } = false;

        public ConfirmDialog(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            return;
        }
    }
}
