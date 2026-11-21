using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace DevSnippetManager.ViewModels
{
    public partial class TagFilterItem : ObservableObject
    {
        public string Name { get; }
        public int Count { get; }

        [ObservableProperty]
        private bool _isSelected;

        public TagFilterItem(string name, int count)
        {
            Name = name;
            Count = count;
        }
    }
}
