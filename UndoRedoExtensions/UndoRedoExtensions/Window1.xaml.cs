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
using System.Windows.Shapes;

namespace UndoRedoExtensions
{
    /// <summary>
    /// Window1.xaml の相互作用ロジック
    /// </summary>
    public partial class Window1 : Window
    {
        public TimeSpan? SelectedStart => StartCombo.SelectedItem as TimeSpan?;
        public TimeSpan? SelectedEnd => EndCombo.SelectedItem as TimeSpan?;
        public string Memo => MemoBox.Text ?? string.Empty;

        public Window1()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedStart == null || SelectedEnd == null)
            {
                MessageBox.Show("開始と終了を選択してください。");
                return;
            }
            DialogResult = true;
        }
    }
}
