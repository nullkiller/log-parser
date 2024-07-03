using System;
using System.Collections;
using System.Collections.Generic;
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
        private List<LogItem> list;
        private string fileName;

        public MainWindow()
        {
            InitializeComponent();

            fileName = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault();

            if (string.IsNullOrEmpty(fileName))
            {
                var dlg = new OpenFileDialog { Multiselect = false };

                if (dlg.ShowDialog() != true)
                {
                    Close();
                }

                fileName = dlg.FileName;
            }

            RefreshList();
        }

        private void txtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            lstLog.ItemsSource = Filter(list, txtSearch.Text);
        }

        private List<LogItem> Filter(List<LogItem> list, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return list;
            }

            return list
                .Select(
                    i => new LogItem(i.Header)
                        {
                            Children = Filter(i.Children, text),
                            
                        })
                .Where(i => i.LinesCount > 0 || i.Children.Any() || i.Header.Contains(text))
                .ToList();
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshList();
        }

        private void RefreshList()
        {
            var patterns = File.ReadAllText("patterns.txt");
            var logFile = new DetachedFile(fileName, long.Parse(txtPosition.Text), 0);//int.Parse(txtLines.Text)
            using (var stream = logFile.CreateStream())
            {
                list = logParser.ParseLog(stream, patternsParser.GetPatterns(patterns));
            }

            GC.Collect();

            lstLog.ItemsSource = Filter(list, txtSearch.Text);
        }
    }
}
