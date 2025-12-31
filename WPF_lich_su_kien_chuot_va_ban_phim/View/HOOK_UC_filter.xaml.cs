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
    /// Interaction logic for HOOK_UC_filter.xaml
    /// </summary>
    public partial class HOOK_UC_filter : UserControl
    {
        private double ScaleFactor => SystemParameters.PrimaryScreenHeight / 1080.0;
        public HOOK_UC_filter()
        {
            InitializeComponent();
            // Scale toàn bộ UI sau khi load
            this.Loaded += HOOK_UC_filter_Loaded;
        }
        private void HOOK_UC_filter_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyScale(this);
        }

        private void ApplyScale(FrameworkElement parent)
        {
            double factor = ScaleFactor;

            foreach (FrameworkElement child in LogicalTreeHelper.GetChildren(parent).OfType<FrameworkElement>())
            {
                // Scale Width / Height nếu đã set
                if (!double.IsNaN(child.Width))
                    child.Width *= factor;
                if (!double.IsNaN(child.Height))
                    child.Height *= factor;

                // Scale Margin
                child.Margin = new Thickness(
                    child.Margin.Left * factor,
                    child.Margin.Top * factor,
                    child.Margin.Right * factor,
                    child.Margin.Bottom * factor
                );

                // Recursively scale children
                if (child is Panel || child is ContentControl || child is UserControl)
                {
                    ApplyScale(child);
                }
            }
        }
    }

}
