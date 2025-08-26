using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace UndoRedoExtensions
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<RowData> Rows { get; } = new();
        public List<TimeSpan> TimeOptions { get; } = new();         // 00:00～24:00 まで 30 分刻み

        private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);

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
                    RebuildWorkingTimeOptions();
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
                    RebuildWorkingTimeOptions();
                }
            }
        }

        public string _summaryText = string.Empty;
        public string SummaryText
        {
            get => _summaryText;
            set
            {
                if (_summaryText != value)
                {
                    _summaryText = value;
                    OnPropertyChanged();
                    IsSummaryEmpty = string.IsNullOrWhiteSpace(_summaryText);

                    if (!_isBuildingSummary)
                    {
                        ApplySummaryToRows(_summaryText);
                    }
                }
            }
        }

        // 追加：循環防止フラグ
        private bool _isBuildingSummary = false; // Rows -> SummaryText へまとめ直し中か？

        private bool _isInternalEdit = false;  // VM内部の連動更新中フラグ


        public ObservableCollection<RowData> CompressedRows { get; } = new();

        private bool _isSummaryEmpty = true;
        public bool IsSummaryEmpty
        {
            get => _isSummaryEmpty;
            
            set
            {
                if (_isSummaryEmpty != value)
                {
                    _isSummaryEmpty = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<TimeSpan> WorkingTimeOptions { get; } = new();              // ★追加：勤務時間の候補


        public MainViewModel()
        {
            for (var t = TimeSpan.Zero; t < TimeSpan.FromDays(1); t += Step)
                TimeOptions.Add(t);

            RebuildRowsFromWindow(); // 初期 8:00–19:00 で構築（23 行）
            RebuildWorkingTimeOptions();       // ★追加：勤務時間の候補も初期化
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

        private void RebuildWorkingTimeOptions()
        {
            WorkingTimeOptions.Clear();
            // 例：開始～終了まで 30 分刻み（End ちょうども含める）
            for (var t = WorkingStart; t <= WorkingEnd; t += Step)
                WorkingTimeOptions.Add(t);
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isInternalEdit) return; // 連動更新の再帰を防止

            if (sender is not RowData row) { UpdateSummary(); return; }
            var idx = Rows.IndexOf(row);
            if (idx < 0) { UpdateSummary(); return; }

            if (e.PropertyName == nameof(RowData.Start))
            {
                row.Start = Snap(row.Start);
                if (row.End <= row.Start)
                    row.End = Min(row.Start + Step, WorkingEnd);

                // ※今回の仕様では Start 変更時は何もしない（必要なら前行のEndを合わせる処理を追加可）
            }
            else if (e.PropertyName == nameof(RowData.End))
            {
                row.End = Snap(row.End);
                if (row.End <= row.Start)
                    row.End = Min(row.Start + Step, WorkingEnd);

                // ★ ここがポイント：次行の Start を合わせる
                AlignNextRowStart(idx);
            }

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
            _isBuildingSummary = true;
            try
            {
                var sb = new StringBuilder();

                TimeSpan? blockStart = null;
                TimeSpan? blockEnd = null;
                string? blockText = null;

                void FlushBlock()
                {
                    if (blockStart.HasValue && blockEnd.HasValue && !string.IsNullOrWhiteSpace(blockText))
                    {
                        sb.AppendLine($"{blockStart:hh\\:mm}-{blockEnd:hh\\:mm} {blockText}");
                    }
                    blockStart = blockEnd = null;
                    blockText = null;
                }

                foreach (var r in Rows)
                {
                    var t = string.IsNullOrWhiteSpace(r.Text) ? null : r.Text;

                    if (t == null)
                    {
                        // テキストなし → 進行中ブロックを確定
                        FlushBlock();
                        continue;
                    }

                    if (blockText == null)
                    {
                        // 新規ブロック開始
                        blockText = t;
                        blockStart = r.Start;
                        blockEnd = r.End;
                    }
                    else if (t == blockText && blockEnd == r.Start)
                    {
                        // 同じテキストが連続しているのでブロック拡張
                        blockEnd = r.End;
                    }
                    else
                    {
                        // テキストが変わった → 既存ブロック確定、新規開始
                        FlushBlock();
                        blockText = t;
                        blockStart = r.Start;
                        blockEnd = r.End;
                    }
                }
                // 最後のブロックを確定
                FlushBlock();

                SummaryText = sb.ToString();
                UpdateCompressedRows();
            }
            finally
            {
                _isBuildingSummary = false;
            }
        }

        private void ApplySummaryToRows(string summary)
        {
            // 事前に必要な範囲を計算
            var rx = new Regex(@"^\s*(\d{1,2}):(\d{2})\s*-\s*(\d{1,2}):(\d{2})\s*(.*)$");
            var lines = summary.Replace("\r\n", "\n").Split('\n');

            TimeSpan? minStart = null;
            TimeSpan? maxEnd = null;

            var parsed = new List<(TimeSpan start, TimeSpan end, string text)>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var m = rx.Match(line);
                if (!m.Success) continue;

                if (!int.TryParse(m.Groups[1].Value, out var sh)) continue;
                if (!int.TryParse(m.Groups[2].Value, out var sm)) continue;
                if (!int.TryParse(m.Groups[3].Value, out var eh)) continue;
                if (!int.TryParse(m.Groups[4].Value, out var em)) continue;

                var content = (m.Groups[5].Value ?? string.Empty).Trim();

                var s = Snap(new TimeSpan(sh, sm, 0));
                var e = Snap(new TimeSpan(eh, em, 0));
                if (e <= s) continue;

                parsed.Add((s, e, content));

                minStart = !minStart.HasValue ? s : (s < minStart ? s : minStart);
                maxEnd = !maxEnd.HasValue ? e : (e > maxEnd ? e : maxEnd);
            }

            // 入力が空/不正なら、左はクリアだけして終了
            if (parsed.Count == 0)
            {
                // Summaryが空/不正 → 30分刻みに戻す
                RebuildRowsFromWindow();  // 既存のデフォルト構築:contentReference[oaicite:1]{index=1}
                UpdateSummary();
                return;
            }

            // 窓を必要に応じて拡張（行を再生成）※ WorkingStart/End は既存仕様で再構築します:contentReference[oaicite:3]{index=3}
            if (minStart.HasValue && minStart.Value < WorkingStart) WorkingStart = minStart.Value;
            if (maxEnd.HasValue && maxEnd.Value > WorkingEnd) WorkingEnd = maxEnd.Value;

            // 既存行の購読解除＆クリア
            foreach (var r in Rows) r.PropertyChanged -= Row_PropertyChanged;
            Rows.Clear();

            // 区間ごとに1行ずつ作成（= 30分刻みには展開しない）
            foreach (var (s, e, content) in parsed)
            {
                var row = new RowData { Start = s, End = e, Text = content }; // RowData は INotifyPropertyChanged 済:contentReference[oaicite:2]{index=2}
                row.PropertyChanged += Row_PropertyChanged;
                Rows.Add(row);
            }

            UpdateSummary(); // 左→右のまとめを作る（後述の「圧縮」版）
        }

        private void UpdateCompressedRows()
        {
            CompressedRows.Clear();

            TimeSpan? blockStart = null;
            TimeSpan? blockEnd = null;
            string? blockText = null;

            void FlushBlock()
            {
                if (blockStart.HasValue && blockEnd.HasValue && !string.IsNullOrWhiteSpace(blockText))
                {
                    CompressedRows.Add(new RowData { Start = blockStart.Value, End = blockEnd.Value, Text = blockText });
                }
                blockStart = blockEnd = null;
                blockText = null;
            }

            foreach (var r in Rows)
            {
                var t = string.IsNullOrWhiteSpace(r.Text) ? null : r.Text;

                if (t == null)
                {
                    FlushBlock();
                    continue;
                }

                if (blockText == null)
                {
                    blockText = t;
                    blockStart = r.Start;
                    blockEnd = r.End;
                }
                else if (t == blockText && blockEnd == r.Start)
                {
                    blockEnd = r.End;
                }
                else
                {
                    FlushBlock();
                    blockText = t;
                    blockStart = r.Start;
                    blockEnd = r.End;
                }
            }
            FlushBlock();
        }

        private void AlignNextRowStart(int idx)
        {
            if (idx < 0 || idx >= Rows.Count - 1) return;

            var cur = Rows[idx];
            var next = Rows[idx + 1];

            // 次行の Start を現在行の End に合わせる
            var newStart = Snap(cur.End);
            if (next.Start != newStart)
            {
                _isInternalEdit = true;
                try
                {
                    next.Start = newStart;

                    // 安全策：もし End <= Start になってしまったら、少なくとも Step だけ確保
                    if (next.End <= next.Start)
                    {
                        next.End = Min(next.Start + Step, WorkingEnd);
                    }
                }
                finally
                {
                    _isInternalEdit = false;
                }
            }
        }

        public void InsertRowAfter(RowData anchor, TimeSpan start, TimeSpan end, string text)
        {
            start = Snap(start);
            end = Snap(end);
            if (end <= start) end = start + TimeSpan.FromMinutes(30);

            var idx = Rows.IndexOf(anchor);
            if (idx < 0) idx = Rows.Count - 1;

            var newRow = new RowData { Start = start, End = end, Text = text };
            newRow.PropertyChanged += Row_PropertyChanged; // 既存の更新連動を効かせる:contentReference[oaicite:3]{index=3}:contentReference[oaicite:4]{index=4}
            Rows.Insert(idx + 1, newRow);
            UpdateSummary(); // 右側まとめも更新:contentReference[oaicite:5]{index=5}
        }



        // クリップボードにコピー（TextBlockは選択不可のため）
        public void CopyAllToClipboard() => Clipboard.SetText(SummaryText);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}