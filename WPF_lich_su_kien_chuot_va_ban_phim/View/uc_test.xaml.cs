using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WPF_lich_su_kien_chuot_va_ban_phim.View
{
    /// <summary>
    /// Interaction logic for uc_test.xaml
    /// </summary>
    public partial class uc_test : UserControl
    {
        public string NameValue { get; set; }
        public uc_test()
        {
            InitializeComponent();
        }
       
        private void my_func_click_show_text_from_user_control_of_win(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("this is user control messeger: " + NameValue);
        }
    }
}
