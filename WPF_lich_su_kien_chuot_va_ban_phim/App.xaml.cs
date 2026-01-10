using System.Windows;
using System.Windows.Input;

namespace WPF_lich_su_kien_chuot_va_ban_phim
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            EventManager.RegisterClassHandler(
                typeof(Window),
                Keyboard.PreviewKeyDownEvent,
                new KeyEventHandler(OnPreviewKeyDown));

            base.OnStartup(e);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true; // ❌ chặn Enter trên toàn app
            }
        }
    }
}
