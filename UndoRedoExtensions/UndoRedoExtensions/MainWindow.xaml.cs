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

namespace UndoRedoExtensions
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.CopyAllToClipboard();
        }

        private void InsertRowMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            // 右クリックされた行(RowData)を取得
            if (sender is not MenuItem mi) return;
            if (mi.DataContext is not RowData anchor) return;

            // 挿入ダイアログをVMを参照するように開く（TimeOptionsを使うため）
            var dlg = new Window1()
            {
                Owner = this,
                DataContext = vm
            };

            // 事前埋め（任意）：開始=anchor.End、終了=（次行があれば次行.Start、それ以外は anchor.End+30分）
            var idx = vm.Rows.IndexOf(anchor);
            var defaultStart = anchor.End;
            var defaultEnd = (idx >= 0 && idx < vm.Rows.Count - 1)
                               ? vm.Rows[idx + 1].Start
                               : anchor.End + TimeSpan.FromMinutes(30);

            // TimeOptions の中から一致項目を選択
            dlg.Loaded += (_, __) =>
            {
                dlg.StartCombo.SelectedItem = defaultStart;
                dlg.EndCombo.SelectedItem = defaultEnd;
                dlg.MemoBox.Text = string.Empty;
            };

            if (dlg.ShowDialog() == true)
            {
                var start = dlg.SelectedStart!.Value;
                var end = dlg.SelectedEnd!.Value;
                var memo = dlg.Memo;

                vm.InsertRowAfter(anchor, start, end, memo); // VMメソッドへ委譲:contentReference[oaicite:10]{index=10}
            }
        }
    }
}