using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NanoCadIO.BlockIO
{
    // 1. Класс для хранения данных блока при экспорте/импорте
    public class BlockData
    {
        public string Handle { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public Dictionary<string, string> Attributes { get; set; } = new();
        public Dictionary<string, string> DynamicProperties { get; set; } = new();
    }

    // 2. Класс для чекбоксов в панели
    public class BlockSelectionItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isInSelection;
        private int _instanceCount;
        private bool _isDynamic;
        private bool _hasAttributes;

        public string BlockName { get; set; } = string.Empty;

        public int InstanceCount
        {
            get => _instanceCount;
            set
            {
                if (_instanceCount == value) return;
                _instanceCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public bool IsDynamic
        {
            get => _isDynamic;
            set
            {
                if (_isDynamic == value) return;
                _isDynamic = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public bool HasAttributes
        {
            get => _hasAttributes;
            set
            {
                if (_hasAttributes == value) return;
                _hasAttributes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        /// <summary>Присутствует ли тип блока в текущем выделении чертежа.</summary>
        public bool IsInSelection
        {
            get => _isInSelection;
            set
            {
                if (_isInSelection == value) return;
                _isInSelection = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Текст, отображаемый рядом с чекбоксом: [D,A] Имя (кол-во).</summary>
        public string DisplayName
        {
            get
            {
                var badges = new List<string>();
                if (IsDynamic) badges.Add("D");
                if (HasAttributes) badges.Add("A");
                var badgeText = badges.Count > 0 ? $"[{string.Join(",", badges)}] " : string.Empty;
                var countText = InstanceCount > 0 ? $" ({InstanceCount})" : string.Empty;
                return $"{badgeText}{BlockName}{countText}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // 3. Результат импорта из Excel — для отображения в панели
    public class ImportResult
    {
        public int UpdatedCount { get; set; }
        public int NotFoundCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Errors { get; } = new();
        public int TotalRowsProcessed => UpdatedCount + NotFoundCount + ErrorCount;
    }
}
