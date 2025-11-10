using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WPF_lich_su_kien_chuot_va_ban_phim.View;

namespace WPF_lich_su_kien_chuot_va_ban_phim
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void btn_test_Click(object sender, RoutedEventArgs e)
        {
            myctrl.Content = new uc_test();

        }
    }
}