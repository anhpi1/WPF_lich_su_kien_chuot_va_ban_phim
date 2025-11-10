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
        string data;
        uc_test my_user_control;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btn_test_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("1");

        }


        private void my_btn_show_user_control(object sender, RoutedEventArgs e)
        {
            my_user_control = new uc_test();
            myctrl.Content = my_user_control;
        }

        private void my_func_click_show_text_of_user_control_from_win(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("this is win messeger: "+my_user_control.my_tb_1.Text);
        }

        private void my_func_set_data_to_NameValue_of_user_control_from_win(object sender, RoutedEventArgs e)
        {
            my_user_control.NameValue = my_tb_1.Text;
        }

    }
}