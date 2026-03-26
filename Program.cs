using RecordClient.Views;

namespace RecordClient
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            bool isAutoStart = args.Contains("--autostart");

            var app = new App();
            app.InitializeComponent();
            app.Run(new MainWindow(isAutoStart));
        }
    }
}
