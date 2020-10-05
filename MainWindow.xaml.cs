using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.WindowsAPICodePack.Dialogs;
using V3Lib.Spc;

namespace FlashbackLight
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private struct EditorTrackingInfo
        {
            public object Editor;
            public string OriginArchivePath;
            public List<string> SubfileNameHistory;
        }

        private readonly DirectoryInfo AppTempDirInfo;
        private DirectoryInfo CurrentWorkspaceDirInfo;
        private FileInfo CurrentArchiveFileInfo;
        private readonly Dictionary<string, EditorTrackingInfo> ActiveFileDatabase; // Key is editor temporary scratch directory

        public MainWindow()
        {
            InitializeComponent();

            // Setup text encoding so we can use Shift-JIS text later on
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Create the temporary directory for our app if it doesn't already exist
            string tempDir = Path.Combine(Path.GetTempPath(), "FlashbackLight");
            AppTempDirInfo = Directory.CreateDirectory(tempDir);

            ActiveFileDatabase = new Dictionary<string, EditorTrackingInfo>();

            // Load config files
            Config.FileAssociationConfig.Load();
        }

        public void OnMainWindowClosing(object sender, CancelEventArgs e)
        {
            // Warn if closing with active files
            if (ActiveFileDatabase.Count > 0)
            {
                if (MessageBox.Show($"You have {ActiveFileDatabase.Count} files currently open for editing, are you sure you want to close?", "Close open files?", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            foreach (var activeEditor in ActiveFileDatabase.Values)
            {
                if (activeEditor.Editor is Window editorWindow)
                {
                    editorWindow.Close();
                }
                else if (activeEditor.Editor is Process editorProcess)
                {
                    if (!editorProcess.HasExited)
                    {
                        if (editorProcess.CloseMainWindow())
                        {
                            editorProcess.WaitForExit();
                        }
                        else
                        {
                            MessageBoxResult result = MessageBox.Show($"Unable to send close signal to {editorProcess.ProcessName}, forcibly close the process?", "Forcibly close?", MessageBoxButton.YesNoCancel);
                            if (result == MessageBoxResult.Yes)
                                editorProcess.Kill();
                            else if (result == MessageBoxResult.No)
                                continue;
                            else
                            {
                                e.Cancel = true;
                                return;
                            }
                        }
                    }
                    editorProcess.Close();
                }
            }

            //Config.FileAssociationConfig.AssociationList.Add(".stx", new List<Config.FileAssociationConfig.FileAssociation>
            //{
            //    new Config.FileAssociationConfig.FileAssociation
            //    {
            //        TranslationSteps = new List<string>()
            //        {
            //            @"external:D:\jpmac\Documents\GitHub\DRV3-Sharp\~Build\Release\StxTool.exe"
            //        },
            //        EditorProgram = @"external:C:\Program Files\Notepad++\notepad++.exe"
            //    }
            //});
            //Config.FileAssociationConfig.AssociationList.Add(".wrd", new List<Config.FileAssociationConfig.FileAssociation>
            //{
            //    new Config.FileAssociationConfig.FileAssociation
            //    {
            //        TranslationSteps = new List<string>(),
            //        EditorProgram = $"internal:{typeof(Editors.WrdEditor).FullName}"
            //    }
            //});
            Config.FileAssociationConfig.Save();
        }

        /// <summary>
        /// Clears the contents of <see cref="FileSystemListView"/> and repopulates it with the directories and files in the provided directory.
        /// </summary>
        /// <param name="newDir">The new directory whose contents will populate the view.</param>
        public void PopulateFileSystemListView(string newDir)
        {
            FileSystemListView.Items.Clear();

            CurrentWorkspaceDirInfo = new DirectoryInfo(newDir);

            TextBlock upDirTb = new TextBlock();
            upDirTb.Text = "..";
            upDirTb.Foreground = Brushes.Blue;
          
            FileSystemListView.Items.Add(upDirTb);

            // First, populate list with directories (color them blue to differentiate)
            foreach (var dir in CurrentWorkspaceDirInfo.EnumerateDirectories())
            {
                TextBlock tb = new TextBlock();
                tb.Text = dir.Name;
                tb.Foreground = Brushes.Blue;
                FileSystemListView.Items.Add(tb);
            }
            // Second, populate list with files
            foreach (var file in CurrentWorkspaceDirInfo.EnumerateFiles())
            {
                // Don't show hidden or system files, for safety reasons
                var attr = File.GetAttributes(file.FullName);
                if (!attr.HasFlag(FileAttributes.System) && !attr.HasFlag(FileAttributes.Hidden))
                {
                    TextBlock tb = new TextBlock();
                    tb.Text = file.Name;
                    FileSystemListView.Items.Add(tb);
                }
            }
        }

        /// <summary>
        /// Clears the contents of <see cref="ArchiveListView"/> and repopulates it with the directories and files in the provided directory.
        /// </summary>
        /// <param name="file">The archive file whose contents will populate the view.</param>
        public void PopulateArchiveListView(string file)
        {
            CurrentArchiveFileInfo = new FileInfo(file);
            SpcFile tempSpc = new SpcFile();
            tempSpc.Load(CurrentArchiveFileInfo.FullName);

            ArchiveListView.Items.Clear();

            foreach (var subfile in tempSpc.Subfiles)
            {
                TextBlock tb = new TextBlock();
                tb.Text = subfile.Name;
                tb.MouseRightButtonDown += GenerateSubfileContextMenu;
                ArchiveListView.Items.Add(tb);
            }
        }

        private void FileSystemListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender == null || !(sender is ListView))
                return;

            if (!((sender as ListView).SelectedItem is TextBlock originTextBlock))
                return;

            // If the selected TextBlock is the "up directory" then adjust the path accordingly
            if (originTextBlock.Text == "..")
            {
                string parentDir = CurrentWorkspaceDirInfo.Parent.FullName;
                PopulateFileSystemListView(parentDir);
                return;
            }

            // Check if the new path is a directory or file
            string combinedPath = Path.Combine(CurrentWorkspaceDirInfo.FullName, originTextBlock.Text);
            FileAttributes attr = File.GetAttributes(combinedPath);
            if (attr.HasFlag(FileAttributes.Directory))
            {
                PopulateFileSystemListView(combinedPath);
                return;
            }
            // Don't operate on system or hidden files for safety reasons
            else if (!attr.HasFlag(FileAttributes.System) && !attr.HasFlag(FileAttributes.Hidden))
            {
                if (combinedPath.ToLowerInvariant().EndsWith(".spc"))
                {
                    PopulateArchiveListView(combinedPath);
                }
                else
                {
                    // If we might be able to open this file directly, try it
                    //ArchiveListView_MouseDoubleClick(sender, e);
                }
            }
        }

        private void ArchiveListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton.HasFlag(MouseButtonState.Pressed)) return;
            OpenSelectedArchiveSubfile(sender, 0);
        }

        private void OpenSelectedArchiveSubfile(object sender, int associationIndex = 0)
        {
            if (sender == null || !(sender is ListView))
                return;

            if (!((sender as ListView).SelectedItem is TextBlock selectedTextBlock))
                return;


            // Open an editor based on target file type
            string targetFileExt = Path.GetExtension(selectedTextBlock.Text).ToLowerInvariant();

            // Throw an error if we can't find a valid editor 
            if (!Config.FileAssociationConfig.AssociationList.ContainsKey(targetFileExt))
                throw new NotImplementedException($"The filetype {targetFileExt} is not supported.");

            // Create the inner temp directory
            //string trackingHash = (CurrentArchivePath + originTextBlock.Text).GetHashCode().ToString("X8");
            string trackingHash = Path.GetRandomFileName();
            string generatedTempDir = Path.Combine(AppTempDirInfo.FullName, trackingHash);
            //string tempFileLocation = Path.Combine(generatedTempDir, originTextBlock.Text);
            Directory.CreateDirectory(generatedTempDir);

            // Extract the subfile to a temporary folder
            SpcFile temp = new SpcFile();
            temp.Load(CurrentArchiveFileInfo.FullName);
            temp.ExtractSubfile(selectedTextBlock.Text, generatedTempDir);


            // Default to the first association in the list
            var association = Config.FileAssociationConfig.AssociationList[targetFileExt][associationIndex];

            // Process translation steps, if any
            List<string> translatedOutputHistory = new List<string>();
            translatedOutputHistory.Add(selectedTextBlock.Text);
            foreach (string translationStep in association.TranslationSteps)
            {
                object resolvedTranslator = Config.FileAssociationConfig.ResolveInternalExternal(translationStep) as Process;

                // Run the translation step
                if (resolvedTranslator is Window)
                {
                    Window translatorWindow = resolvedTranslator as Window;

                    // Finally, open the target editor window as blocking
                    translatorWindow.ShowDialog();
                }
                else if (resolvedTranslator is Process)
                {
                    Process translatorProcess = resolvedTranslator as Process;

                    // Setup the target process' launch args
                    translatorProcess.StartInfo.Arguments = Path.Combine(generatedTempDir, translatedOutputHistory.Last());

                    translatorProcess.Start();
                    translatorProcess.WaitForExit();
                }
                else
                {
                    // If we get here, there's been an error and we should abort and clean up
                    Directory.Delete(generatedTempDir, true);
                }

                // Check for any extra files and use the first that isn't our starting file as the translated output
                List<string> newFiles = new List<string>();
                foreach (var file in Directory.EnumerateFiles(generatedTempDir))
                {
                    FileInfo tempInfo = new FileInfo(file);

                    if (!translatedOutputHistory.Contains(tempInfo.Name))
                    {
                        newFiles.Add(tempInfo.Name);
                    }
                }

                // TODO: If multiple new files are generated, prompt the user for which file to use as the output
                foreach (string newFile in newFiles)
                {
                    translatedOutputHistory.Add(newFile);
                    break;
                }
            }

            // Get editor window if association is internal, launch external program otherwise
            object resolvedEditor = Config.FileAssociationConfig.ResolveInternalExternal(association.EditorProgram);

            // Add the target editor to our tracking database (use the non-translated subfile name here!!!)
            ActiveFileDatabase.Add(generatedTempDir, new EditorTrackingInfo { 
                Editor = resolvedEditor,
                OriginArchivePath = CurrentArchiveFileInfo.FullName,
                SubfileNameHistory = translatedOutputHistory });

            if (resolvedEditor is Window)
            {
                Window editorWindow = resolvedEditor as Window;

                // Subscribe to the editor's closing event so we can rebuild its target archive and clean up any temporary files
                editorWindow.Closing += OnEditorWindowClosed;

                // Finally, open the target editor window
                editorWindow.Show();
            }
            else if (resolvedEditor is Process)
            {
                Process editorProcess = resolvedEditor as Process;

                // Subscribe to the editor's closing event so we can rebuild its target archive and clean up any temporary files
                editorProcess.EnableRaisingEvents = true;
                editorProcess.Exited += OnEditorProcessExited;

                // Setup the target process' launch args
                editorProcess.StartInfo.Arguments = generatedTempDir + Path.DirectorySeparatorChar + translatedOutputHistory.Last();

                // Finally, open the target editor window
                editorProcess.Start();
            }
            else
            {
                // If we get here, there's been an error and we should abort and clean up
                Directory.Delete(generatedTempDir, true);
                ActiveFileDatabase.Remove(generatedTempDir);
            }
        }

        private void GenerateSubfileContextMenu(object sender, MouseButtonEventArgs e)
        {
            if (sender == null) return;
            if (!(sender is TextBlock senderTextBlock)) return;

            if (senderTextBlock.ContextMenu == null) senderTextBlock.ContextMenu = new ContextMenu();

            senderTextBlock.ContextMenu.Items.Clear();

            // Dynamically generate the ContextMenu for the control
            string fileExt = Path.GetExtension(senderTextBlock.Text);
            if (!Config.FileAssociationConfig.AssociationList.ContainsKey(fileExt)) return;
            foreach (var association in Config.FileAssociationConfig.AssociationList[fileExt])
            {
                MenuItem item = new MenuItem() { Header = association.EditorProgram };
                item.Click += SubfileContextMenuItem_Click;
                senderTextBlock.ContextMenu.Items.Add(item);
            }
        }

        private void SelectWorkspaceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog openFile = new CommonOpenFileDialog();
            openFile.IsFolderPicker = true;

            if (openFile.ShowDialog().HasFlag(CommonFileDialogResult.Cancel)) return;

            string selectedDir = openFile.FileName;
            if (!string.IsNullOrWhiteSpace(selectedDir) && File.GetAttributes(selectedDir).HasFlag(FileAttributes.Directory))
            {
                PopulateFileSystemListView(selectedDir);
            }
        }

        private void SubfileContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender == null) return;
            if (!(sender is MenuItem senderMenuItem)) return;
            if ((ArchiveListView.SelectedItem == null) || !(ArchiveListView.SelectedItem is TextBlock selectedTextBlock)) return;
            if (selectedTextBlock.ContextMenu == null || selectedTextBlock.ContextMenu.Items.Count == 0) return;

            int index = selectedTextBlock.ContextMenu.Items.IndexOf(senderMenuItem);
            OpenSelectedArchiveSubfile(ArchiveListView, index);
        }

        private void OnEditorWindowClosed(object sender, CancelEventArgs e)
        {
            // The statement "e.Cancel = true;" will cancel the closure of the target window

            // Find the matching window instance in our tracking database
            Window senderWindow = sender as Window;
            if (sender == null)
                return;

            var matchingEntry = ActiveFileDatabase.Where(e => (e.Value.Editor == senderWindow)).First();


            // TODO: Do stuff like rebuild origin archive here


            // Delete the temp file for that directory after we've finished rebuilding the origin archive, etc.
            Directory.Delete(matchingEntry.Key, true);
            ActiveFileDatabase.Remove(matchingEntry.Key);
        }

        private void OnEditorProcessExited(object sender, EventArgs e)
        {
            // Find the matching window instance in our tracking database
            Process senderProcess = sender as Process;
            if (sender == null)
                return;

            var matchingEntry = ActiveFileDatabase.Where(e => (e.Value.Editor == senderProcess)).First();


            // TODO: Do stuff like rebuild origin archive here


            // Delete the temp file for that directory after we've finished rebuilding the origin archive, etc.
            Directory.Delete(matchingEntry.Key, true);
            ActiveFileDatabase.Remove(matchingEntry.Key);
        }
    }
}
