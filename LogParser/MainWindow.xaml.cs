using System;
using System.IO;
using System.Linq;
using System.Windows;
using LogParser.Core;
using Microsoft.Win32;

namespace LogParser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private PatternParser patternsParser = new PatternParser();
        private LogItemsParser logParser = new LogItemsParser();

        public MainWindow()
        {
            InitializeComponent();

            string fileName = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault();

            if (string.IsNullOrEmpty(fileName))
            {
                var dlg = new OpenFileDialog { Multiselect = false };

                if (dlg.ShowDialog() != true)
                {
                    Close();
                }

                fileName = dlg.FileName;
            }

            string log;

            using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(fileStream))
            {
                log = reader.ReadToEnd();
            }

            var patterns = File.ReadAllText("patterns.txt");

            lstLog.ItemsSource = logParser.ParseLog(log, patternsParser.GetPatterns(patterns));
        }
    }
}
