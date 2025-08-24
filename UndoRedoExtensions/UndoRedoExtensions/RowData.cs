using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace UndoRedoExtensions
{
    public class RowData : INotifyPropertyChanged
    {
        private TimeSpan _start;
        private TimeSpan _end;
        private string _text = string.Empty;

        // ユーザーが手入力したかどうかを保持（手入力済みなら自動書き換えしない）
        private bool _isTextUserEdited = false;

        public TimeSpan Start
        {
            get => _start;
            set { if (_start != value) { _start = value; OnPropertyChanged(); UpdateAutoText(); } }
        }

        public TimeSpan End
        {
            get => _end;
            set { if (_end != value) { _end = value; OnPropertyChanged(); } }
        }

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    _isTextUserEdited = true;        // 手入力が入ったら以後は自動更新しない
                    OnPropertyChanged();
                }
            }
        }

        // VMから「自動でテキストを入れ直したい」時に呼ぶ（未編集のときのみ）
        public void SetAutoText(string s)
        {
            if (!_isTextUserEdited && _text != s)
            {
                _text = s;
                OnPropertyChanged(nameof(Text));
            }
        }

        private void UpdateAutoText()
        {
            // Start が変わったとき、未編集なら「HH:mm開始」を自動で入れる
            SetAutoText($"");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
