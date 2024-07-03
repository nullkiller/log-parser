using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LogParser.Core
{
    public class LogItem
    {
        public List<LogItem> Children { get; set; }

        public string Header { get; set; }

        public Lazy<IEnumerable<string>> Lines { get; }

        public List<object> Items => this.Lines.Value.Cast<object>().Concat(Children.Cast<object>()).ToList();

        public DetachedFile SubStream { get; internal set; }
        
        public int LinesCount { get; internal set; }

        public LogItem(string header, DetachedFileStream log)
        {
            Children = new List<LogItem>();
            Header = header;
            Lines = new Lazy<IEnumerable<string>>(() => this.LinesCount > 0 ? this.SubStream.ReadLines(LinesCount) : Array.Empty<string>());
            SubStream = log.CreateSubFile();
        }

        public LogItem(string header)
        {
            Header = header;
            Children = new List<LogItem>();
        }
    }

    public class PatternHierarchyItem
    {
        public string SpacingBefore { get; set; }

        public Pattern Pattern { get; set; }
    }

    public class LogHierarchyItem
    {
        public LogItem LogItem { get; set; }

        public Pattern Pattern { get; set; }
    }

    public class DetachedFile : IEnumerable<string>
    {
        private readonly string fileName;
        private readonly long offset;
        private readonly int lines;

        public DetachedFile(string fileName, long offset, int lines)
        {
            this.fileName = fileName;
            this.offset = offset;
            this.lines = lines;
        }

        public IEnumerator<string> GetEnumerator()
        {
            return CreateStream();
        }

        public DetachedFileStream CreateStream()
        {
            return new DetachedFileStream(this.fileName, this.offset, this.lines);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IReadOnlyList<string> ReadLines(int lines)
        {
            return this.Take(lines).ToList();
        }
    }

    public struct FileLine
    {
        public string Line { get; set; }

        public long NextPosition { get; set; }
    }

    public class DetachedFileStream : IEnumerator<string>
    {
        long position;
        string fileName;
        Queue<FileLine> filePart;
        private readonly int lines;
        private int lineNumber;

        public FileLine CurrentLine { get; private set; }

        public string Current => CurrentLine.Line;

        object IEnumerator.Current => this.Current;

        public DetachedFileStream(string fileName, long position = 0, int lines = 0)
        {
            this.fileName = fileName;
            this.position = position;
            this.lines = lines;
            this.filePart = new Queue<FileLine>();
        }

        public DetachedFile CreateSubFile()
        {
            return new DetachedFile(fileName, this.CurrentLine.NextPosition, 0);
        }

        public void Dispose()
        {
            this.filePart.Clear();
        }

        public bool MoveNext()
        {
            while (true)
            {
                if (filePart.Count > 0)
                {
                    this.CurrentLine = filePart.Dequeue();
                    this.lineNumber++;

                    return this.lines == 0 || this.lineNumber < this.lines;
                }

                using (BufferedStream fileStream = new BufferedStream(
                    File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                using (StreamReader reader = new StreamReader(fileStream))
                {
                    fileStream.Position = position;

                    for (int i = 0; i < 1000; i++)
                    {
                        FileLine fileLine = new FileLine
                        {
                            Line = reader.ReadLine(),
                            NextPosition = fileStream.Position
                        };

                        if (fileLine.Line == null)
                        {
                            break;
                        }

                        filePart.Enqueue(fileLine);
                    }

                    position = fileStream.Position;

                    if (filePart.Count == 0)
                    {
                        return false;
                    }
                }
            }
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }

    public class LogItemsParser
    {
        public List<LogItem> ParseLog(DetachedFileStream log, List<Pattern> patterns)
        {
            List<LogHierarchyItem> patternsHierarchy = new List<LogHierarchyItem>();

            var rootHierarchy = new LogHierarchyItem
            {
                Pattern = new Pattern(""),
                LogItem = new LogItem("", log)
            };

            rootHierarchy.Pattern.Children = patterns;
            patternsHierarchy.Add(rootHierarchy);

            while (log.MoveNext())
            {
                if (!string.IsNullOrWhiteSpace(log.Current))
                {
                    ProcessLogLine(patternsHierarchy, log);
                }
            }

            return rootHierarchy.LogItem.Children;
        }

        private void ProcessLogLine(List<LogHierarchyItem> patternsHierarchy, DetachedFileStream log)
        {
            var logLine = log.Current;

            for (int i = 0; i < patternsHierarchy.Count; i++)
            {
                var pattern = patternsHierarchy[i].Pattern.Children.FirstOrDefault(p => p.Matches(logLine));

                if (pattern != null)
                {
                    var logItem = new LogHierarchyItem
                    {
                        Pattern = pattern,
                        LogItem = new LogItem(logLine, log)
                    };

                    patternsHierarchy[i].LogItem.Children.Add(logItem.LogItem);
                    patternsHierarchy.RemoveRange(i + 1, patternsHierarchy.Count - i - 1);
                    patternsHierarchy.Insert(patternsHierarchy.Count, logItem);

                    return;
                }
            }

            patternsHierarchy.Last().LogItem.LinesCount++;
        }
    }

    public class PatternParser
    {
        public List<Pattern> GetPatterns(string patternsConfig)
        {
            string[] items = patternsConfig.Split('\n', '\r');
            Stack<PatternHierarchyItem> patternsHierarchy = new Stack<PatternHierarchyItem>();
            PatternHierarchyItem root = new PatternHierarchyItem
            {
                SpacingBefore = String.Empty,
                Pattern = new Pattern(string.Empty)
            };

            patternsHierarchy.Push(root);

            foreach (string patternLine in items.Where(t => t != String.Empty))
            {
                var patternText = patternLine.Trim();
                var spacesBefore = patternLine.Substring(0, patternLine.IndexOf(patternText));
                var patternHierarchyItem = new PatternHierarchyItem
                {
                    SpacingBefore = spacesBefore,
                    Pattern = new Pattern(patternText)
                };
                
                while (patternsHierarchy.Count > 0)
                {
                    var topHierarchy = patternsHierarchy.Peek();

                    var isRoot = topHierarchy == root;
                    var isAncestor
                        = patternHierarchyItem.SpacingBefore.StartsWith(topHierarchy.SpacingBefore)
                          && patternHierarchyItem.SpacingBefore.CompareTo(topHierarchy.SpacingBefore) > 0;

                    if (isRoot || isAncestor)
                    {
                        topHierarchy.Pattern.Children.Add(patternHierarchyItem.Pattern);
                        break;
                    }
                    else if (topHierarchy.SpacingBefore.StartsWith(patternHierarchyItem.SpacingBefore))
                    {
                        patternsHierarchy.Pop();
                    }
                    else
                    {
                        throw new Exception("Invalid spacing for " + patternLine);
                    }
                }

                patternsHierarchy.Push(patternHierarchyItem);
            }

            return root.Pattern.Children;
        }
    }

    public class Pattern
    {
        public Pattern(string pattern)
        {
            this.Value = pattern;
            this.Children = new List<Pattern>();
        }

        public string Value { get; set; }

        public List<Pattern> Children { get; set; }

        public LogItem TryGetItem(string lineOfText)
        {
            return null;
        }

        public bool Matches(string logLine)
        {
            return Regex.Match(logLine, Value, RegexOptions.IgnoreCase).Success;
        }
    }
}
