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

    // 2. Класс для чекбоксов в панели (INotifyPropertyChanged нужен для работы галочек)
    public class BlockSelectionItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        
        public string BlockName { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
