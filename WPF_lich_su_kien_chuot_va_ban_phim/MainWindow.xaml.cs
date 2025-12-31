using System.Diagnostics;
using System.Windows;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using WPF_lich_su_kien_chuot_va_ban_phim.View;
using WPF_lich_su_kien_chuot_va_ban_phim.Model;

namespace WPF_lich_su_kien_chuot_va_ban_phim
{
    public partial class MainWindow : Window
    {
        // Độ phân giải gốc bạn thiết kế
        private HOOK__UC_main HOOK_UC_Main;
        private HOOK_UC_filter HOOK_UC_Filter;
        private const double DESIGN_WIDTH = 1920.0;
        private const double DESIGN_HEIGHT = 1080.0;
        private bool is_home_on = true;
        private bool is_setting_on = false;
        private bool is_filter_on = false;


        public MainWindow()
        {
            InitializeComponent();
            
            
         
            // Gắn sự kiện resize
            SizeChanged += MainWindow_SizeChanged;
            Loaded += MainWindow_Loaded;
            HOOK_UC_Main = new HOOK__UC_main();
            HOOK_UC_Filter = new HOOK_UC_filter();
            myctrl.Content = HOOK_UC_Main;

        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyScale();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyScale();
        }

        private void ApplyScale()
        {
            if (RootGrid == null) return;

            double scaleX = ActualWidth / DESIGN_WIDTH;
            double scaleY = ActualHeight / DESIGN_HEIGHT;

            RootGrid.LayoutTransform = new ScaleTransform(scaleX, scaleY);
        }
        private void AnimateFromBottom(UIElement elem)
        {
            // 1️⃣ TranslateTransform (trôi từ dưới lên)
            var tt = elem.RenderTransform as TranslateTransform;
            if (tt == null)
            {
                tt = new TranslateTransform();
                elem.RenderTransform = tt;
            }

            var moveAnim = new DoubleAnimation
            {
                From = 50, // bắt đầu 50px dưới
                To = 0,    // về vị trí chuẩn
                Duration = TimeSpan.FromSeconds(0.6),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            tt.BeginAnimation(TranslateTransform.YProperty, moveAnim);

            // 2️⃣ Fade-in
            elem.Opacity = 0; // reset opacity trước khi animate
            var fadeAnim = new DoubleAnimation
            {
                From = 0,   // mờ hoàn toàn
                To = 1,     // xuất hiện đầy đủ
                Duration = TimeSpan.FromSeconds(0.6),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            elem.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Home_on.Visibility = Visibility.Visible;
            Home_off.Visibility = Visibility.Hidden;
            Home.Foreground = (Brush)new BrushConverter().ConvertFrom("#3b82f6");
            Screen.Source = new BitmapImage(new Uri("img/Home.png", UriKind.Relative));
            Screen_text_header.Text = "Home";
            Screen_text_title.Text = "Welcome to my app!";
            is_home_on = true;
            myctrl.Content = HOOK_UC_Main;

            Setting_on.Visibility = Visibility.Hidden;
            Setting_off.Visibility = Visibility.Visible;
            Setting.Foreground = (Brush)new BrushConverter().ConvertFrom("#FF979DA5");
            is_setting_on = false;

            Filter_on.Visibility = Visibility.Hidden;
            Filter_off.Visibility = Visibility.Visible;
            Filter.Foreground = (Brush)new BrushConverter().ConvertFrom("#FF979DA5");
            is_filter_on = false;

            // Animate trôi từ dưới lên
            AnimateFromBottom(Screen);
            AnimateFromBottom(Screen_text_header);
            AnimateFromBottom(Screen_text_title);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Setting_on.Visibility = Visibility.Visible;
            Setting_off.Visibility = Visibility.Hidden;
            Setting.Foreground = (Brush)new BrushConverter().ConvertFrom("#3b82f6");
            Screen.Source = new BitmapImage(new Uri("img/Setting.png", UriKind.Relative));
            Screen_text_header.Text = "Setting";
            Screen_text_title.Text = "Bạn có thể kiểm soát mọi thứ!";
            is_setting_on = true;

            Home_on.Visibility = Visibility.Hidden;
            Home_off.Visibility = Visibility.Visible;
            Home.Foreground = (Brush)new BrushConverter().ConvertFrom("#FF979DA5");
            is_home_on = false;

            Filter_on.Visibility = Visibility.Hidden;
            Filter_off.Visibility = Visibility.Visible;
            Filter.Foreground = (Brush)new BrushConverter().ConvertFrom("#FF979DA5");
            is_filter_on = false;

            // Animate trôi từ dưới lên
            AnimateFromBottom(Screen);
            AnimateFromBottom(Screen_text_header);
            AnimateFromBottom(Screen_text_title);
        }


        private void home_mouse_leave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (is_home_on) Home.Foreground = (Brush)new BrushConverter().ConvertFrom("#3b82f6");
            else Home.Foreground = (Brush)new BrushConverter().ConvertFrom("#FF979DA5");
            
        }

        private void home_mouse_enter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Home.Foreground = (Brush)new BrushConverter().ConvertFrom("#000000");
        }

        private void Setting_mouse_enter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Setting.Foreground = (Brush)new BrushConverter().ConvertFrom("#000000");
        }

        private void Setting_mouse_leaver(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (is_setting_on) Setting.Foreground = (Brush)new BrushConverter().ConvertFrom("#3b82f6");
            else Setting.Foreground = (Brush)new BrushConverter().ConvertFrom("#FF979DA5");
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Filter_on.Visibility = Visibility.Visible;
            Filter_off.Visibility = Visibility.Hidden;
            Filter.Foreground = (Brush)new BrushConverter().ConvertFrom("#3b82f6");
            Screen.Source = new BitmapImage(new Uri("img/Filter.png", UriKind.Relative));
            Screen_text_header.Text = "Filter";
            Screen_text_title.Text = "Lọc những thứ khó hiểu thành dễ hiểu!";
            is_filter_on = true;
            myctrl.Content = HOOK_UC_Filter;

            Home_on.Visibility = Visibility.Hidden;
            Home_off.Visibility = Visibility.Visible;
            Home.Foreground = (Brush)new BrushConverter().ConvertFrom("#FF979DA5");
            is_home_on = false;

            Setting_on.Visibility = Visibility.Hidden;
            Setting_off.Visibility = Visibility.Visible;
            Setting.Foreground = (Brush)new BrushConverter().ConvertFrom("#FF979DA5");
            is_setting_on = false;

            // Animate trôi từ dưới lên
            AnimateFromBottom(Screen);
            AnimateFromBottom(Screen_text_header);
            AnimateFromBottom(Screen_text_title);
        }

        private void Filter_mouse_enter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Filter.Foreground = (Brush)new BrushConverter().ConvertFrom("#000000");
        }

        private void Filter_mouse_leaver(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (is_filter_on) Filter.Foreground = (Brush)new BrushConverter().ConvertFrom("#3b82f6");
            else Filter.Foreground = (Brush)new BrushConverter().ConvertFrom("#FF979DA5");
        }
    }
}