using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using V3Lib.Wrd;

namespace FlashbackLight.Editors
{
    /// <summary>
    /// Interaction logic for WrdEditor.xaml
    /// </summary>
    public partial class WrdEditor : Window, IFileEditor
    {
        private WrdFile wrd;
        private string wrdPath;
        private WrdStateMachine wrdState;

        public WrdEditor()
        {
            InitializeComponent();
        }

        public void LoadFile(string path)
        {
            wrd = new WrdFile();
            wrd.Load(path);
            wrdPath = path;
        }

        public void SaveFile()
        {
            wrd.Save(wrdPath);
        }
    }

    class WrdStateMachine
    {

    }
}
