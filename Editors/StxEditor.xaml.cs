﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using V3Lib.Stx;

namespace FlashbackLight.Editors
{
    /// <summary>
    /// Interaction logic for StxEditor.xaml
    /// </summary>
    public partial class StxEditor : Window, IFileEditor
    {
        private ObservableCollection<string> Strings;
        private string StxPath;

        public StxEditor()
        {
            InitializeComponent();
        }

        public void LoadFile(string stxFilePath)
        {
            StxFile stx = new StxFile();
            stx.Load(stxFilePath);
            StxPath = stxFilePath;

            Strings = new ObservableCollection<string>(stx.StringTables[0].Strings);
            StringListBox.ItemsSource = Strings;
        }

        public void SaveFile()
        {
            throw new NotImplementedException();
        }

        private void StringMoveUp(object sender, RoutedEventArgs e)
        {
            int selectedIndex = StringListBox.SelectedIndex;

            if (selectedIndex < 0 || selectedIndex >= Strings.Count) return;
            if (selectedIndex - 1 < 0) return;

            string temp = Strings[selectedIndex];
            Strings[selectedIndex] = Strings[selectedIndex - 1];
            Strings[selectedIndex - 1] = temp;

            StringListBox.SelectedIndex = selectedIndex - 1;
        }

        private void StringMoveDown(object sender, RoutedEventArgs e)
        {
            int selectedIndex = StringListBox.SelectedIndex;

            if (selectedIndex < 0 || selectedIndex >= Strings.Count) return;
            if (selectedIndex + 1 >= Strings.Count) return;

            string temp = Strings[selectedIndex];
            Strings[selectedIndex] = Strings[selectedIndex + 1];
            Strings[selectedIndex + 1] = temp;

            StringListBox.SelectedIndex = selectedIndex + 1;
        }

        private void StringAdd(object sender, RoutedEventArgs e)
        {
            int selectedIndex = StringListBox.SelectedIndex;

            if (selectedIndex < 0 || selectedIndex >= Strings.Count) selectedIndex = Strings.Count - 1;
            if (Strings.Count == 0) selectedIndex = 0;

            Strings.Insert(selectedIndex, string.Empty);

            StringListBox.SelectedIndex = selectedIndex;
        }

        private void StringRemove(object sender, RoutedEventArgs e)
        {
            int selectedIndex = StringListBox.SelectedIndex;

            if (selectedIndex < 0 || selectedIndex >= Strings.Count) return;

            Strings.RemoveAt(selectedIndex);

            StringListBox.SelectedIndex = Math.Min(selectedIndex, Strings.Count - 1);
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveFile();
        }

        private void ChangeTableMenuItem_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
