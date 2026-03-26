using RecordClient.Views;
using RecordClient.Views.Controls.Modules;
using System.Windows;

namespace RecordClient.Helpers.Popup
{
    public static class Confirm
    {
        /// <summary>
        /// 확인 창을 띄우고 사용자의 선택을 반환
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        /// <param name="owner">Dialog의 Owner 윈도우 (생략 가능)</param>
        /// <returns>확인(true), 취소(false)</returns>
        public static bool Show(string message, Window? owner = null)
        {
            var dialog = new ConfirmDialog(message)
            {
                Owner = owner ?? Application.Current.MainWindow
            };

            return dialog.ShowDialog() == true && dialog.Result;
        }
    }
}
