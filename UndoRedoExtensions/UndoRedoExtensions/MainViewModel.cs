using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace UndoRedoExtensions
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<RowData> Rows { get; } = new();
        public List<TimeSpan> TimeOptions { get; } = new();

        private static readonly TimeSpan Step = TimeSpan.FromMinutes(30);

        private TimeSpan _workingStart = new(8, 0, 0);  // 初期 8:00
        public TimeSpan WorkingStart
        {
            get => _workingStart;
            set
            {
                if (_workingStart != value)
                {
                    _workingStart = value;
                    OnPropertyChanged();
                    RebuildRowsFromWindow();
                }
            }
        }

        private TimeSpan _workingEnd = new(19, 0, 0);   // 初期 19:00
        public TimeSpan WorkingEnd
        {
            get => _workingEnd;
            set
            {
                if (_workingEnd != value)
                {
                    _workingEnd = value;
                    OnPropertyChanged();
                    RebuildRowsFromWindow();
                }
            }
        }

        private string _summaryText = string.Empty;
        public string SummaryText
        {
            get => _summaryText;
            private set { if (_summaryText != value) { _summaryText = value; OnPropertyChanged(); } }
        }

        public MainViewModel()
        {
            for (var t = TimeSpan.Zero; t < TimeSpan.FromDays(1); t += Step)
                TimeOptions.Add(t);

            RebuildRowsFromWindow(); // 初期 8:00–19:00 で構築（23 行）
        }

        private void RebuildRowsFromWindow()
        {
            foreach (var r in Rows) r.PropertyChanged -= Row_PropertyChanged;
            Rows.Clear();

            var s = WorkingStart;
            while (true)
            {
                var e = s + Step;
                if (e > WorkingEnd) break;

                var row = new RowData { Start = s, End = e };
                // 自動テキスト（未編集なので入る）
                row.SetAutoText($"");

                row.PropertyChanged += Row_PropertyChanged;
                Rows.Add(row);
                s = e;
            }
            UpdateSummary();
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not RowData row) { UpdateSummary(); return; }
            var idx = Rows.IndexOf(row);
            if (idx < 0) { UpdateSummary(); return; }

            if (e.PropertyName == nameof(RowData.Start))
            {
                // Start を 30分刻みに揃え、End を Start+30 に
                row.Start = Snap(row.Start);
                row.End = Min(row.Start + Step, WorkingEnd);
                ReflowFrom(idx + 1, row.End);
            }
            else if (e.PropertyName == nameof(RowData.End))
            {
                row.End = Snap(row.End);
                if (row.End <= row.Start) row.End = Min(row.Start + Step, WorkingEnd);
                ReflowFrom(idx + 1, row.End);
            }
            // Text 変更時も Summary 更新
            UpdateSummary();
        }

        private void ReflowFrom(int startIndex, TimeSpan anchorStart)
        {
            var s = anchorStart;

            // 既存の後続行を更新／はみ出たら削除
            for (int i = startIndex; i < Rows.Count; i++)
            {
                var e = s + Step;
                if (e > WorkingEnd)
                {
                    for (int k = Rows.Count - 1; k >= i; k--)
                    {
                        Rows[k].PropertyChanged -= Row_PropertyChanged;
                        Rows.RemoveAt(k);
                    }
                    UpdateSummary();
                    return;
                }

                if (Rows[i].Start != s) Rows[i].Start = s;
                if (Rows[i].End != e) Rows[i].End = e;

                // 未編集の行には自動テキスト（開始時刻に追随）
                Rows[i].SetAutoText($"");
                s = e;
            }

            // 足りなければ追加
            while (true)
            {
                var e = s + Step;
                if (e > WorkingEnd) break;

                var newRow = new RowData { Start = s, End = e };
                newRow.SetAutoText($"");
                newRow.PropertyChanged += Row_PropertyChanged;
                Rows.Add(newRow);
                s = e;
            }
            UpdateSummary();
        }

        private TimeSpan Snap(TimeSpan t)
        {
            var snapped = new TimeSpan((long)Math.Round(t.Ticks / (double)Step.Ticks) * Step.Ticks);
            if (snapped < TimeSpan.Zero) snapped = TimeSpan.Zero;
            if (snapped > TimeSpan.FromDays(1)) snapped = TimeSpan.FromDays(1);
            return snapped;
        }
        private static TimeSpan Min(TimeSpan a, TimeSpan b) => a <= b ? a : b;

        private void UpdateSummary()
        {
            SummaryText = string.Join(Environment.NewLine,
                Rows.Select(r =>
                    $"{r.Start:hh\\:mm}-{r.End:hh\\:mm} {(string.IsNullOrWhiteSpace(r.Text) ? string.Empty : $"{r.Text}")}"
                )
            );
        }

        // クリップボードにコピー（TextBlockは選択不可のため）
        public void CopyAllToClipboard() => Clipboard.SetText(SummaryText);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}