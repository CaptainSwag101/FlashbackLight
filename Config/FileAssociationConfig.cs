using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Newtonsoft.Json;

namespace FlashbackLight.Config
{
    
    /// <summary>
    /// Contains a dictionary of all editors associated with each game file type/extension.
    /// </summary>
    public static class FileAssociationConfig
    {
        public class FileAssociation
        {
            public List<string> TranslationSteps { get; set; }
            public string EditorProgram { get; set; }
        }

        public static Dictionary<string, List<FileAssociation>> AssociationList = new Dictionary<string, List<FileAssociation>>();

        // Name of the config file
        private const string configFilename = @"Config\file_associations.json";

        /// <summary>
        /// Reads the config file from disk.
        /// </summary>
        public static void Load()
        {
            try
            {
                AssociationList = JsonConvert.DeserializeObject< Dictionary<string, List<FileAssociation>> >(File.ReadAllText(configFilename));
            }
            catch (FileNotFoundException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// Saves the config file to disk.
        /// </summary>
        public static void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(AssociationList, Formatting.Indented);
                File.WriteAllText(configFilename, json);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public static object ResolveInternalExternal(string step)
        {
            // Get editor window if association is internal, external process otherwise
            if (step.StartsWith(@"internal:"))
            {
                string className = step.Substring(@"internal:".Length);

                // Security measure: do not allow instantiating any classes that aren't part of the FlashbackLight root namespace
                if (!className.StartsWith(@"FlashbackLight."))
                {
                    MessageBox.Show($"SECURITY WARNING: {nameof(FileAssociationConfig)} attempted to load an editor class from a non-FlashbackLight namespace {className}, aborting!",
                        "SECURITY WARNING", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return null;
                }

                Type editorType = Type.GetType(className);
                return Activator.CreateInstance(editorType);
            }
            else if (step.StartsWith(@"external:"))
            {
                Process editorProcess = new Process();
                editorProcess.StartInfo.FileName = step.Substring(@"external:".Length);

                return editorProcess;
            }
            else
            {
                return null;
            }
        }
    }
}
