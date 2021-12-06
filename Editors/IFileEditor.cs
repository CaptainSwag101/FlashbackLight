using System;
using System.Collections.Generic;
using System.Text;

namespace FlashbackLight.Editors
{
    interface IFileEditor
    {
        public void LoadFile(string path);
        public void SaveFile();
    }
}
