using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

namespace RefCheck
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void DataGrid_OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var browsable = (e.PropertyDescriptor as PropertyDescriptor)?.Attributes.OfType<BrowsableAttribute>().FirstOrDefault();
            if (!(browsable?.Browsable ?? true))
            {
                e.Cancel = true;
            }
        }
    }
}
