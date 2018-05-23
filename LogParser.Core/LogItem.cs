using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LogParser.Core
{
    public class LogItem
    {
        public List<LogItem> Children { get; set; }

        public string Header { get; set; }

        public List<string> Lines { get; set; }

        public string Text => string.Join(Environment.NewLine, Lines);

        public LogItem(string header)
        {
            Children = new List<LogItem>();
            Lines = new List<string>();
            Header = header;
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

    public class LogItemsParser
    {
        public List<LogItem> ParseLog(string log, List<Pattern> patterns)
        {
            string[] items = log.Split('\n', '\r');
            List<LogHierarchyItem> patternsHierarchy = new List<LogHierarchyItem>();

            var rootHierarchy = new LogHierarchyItem
            {
                Pattern = new Pattern(""),
                LogItem = new LogItem("")
            };

            rootHierarchy.Pattern.Children = patterns;
            patternsHierarchy.Add(rootHierarchy);

            foreach (string logLine in items.Where(t => t != String.Empty))
            {
                ProcessLogLine(patternsHierarchy, logLine);
            }

            return rootHierarchy.LogItem.Children;
        }

        private void ProcessLogLine(List<LogHierarchyItem> patternsHierarchy, string logLine)
        {
            for (int i = 0; i < patternsHierarchy.Count; i++)
            {
                var pattern = patternsHierarchy[i].Pattern.Children.FirstOrDefault(p => p.Matches(logLine));

                if (pattern != null)
                {
                    var logItem = new LogHierarchyItem
                    {
                        Pattern = pattern,
                        LogItem = new LogItem(logLine)
                    };

                    patternsHierarchy[i].LogItem.Children.Add(logItem.LogItem);
                    patternsHierarchy.RemoveRange(i + 1, patternsHierarchy.Count - i - 1);
                    patternsHierarchy.Insert(patternsHierarchy.Count, logItem);

                    return;
                }
            }

            patternsHierarchy.Last().LogItem.Lines.Add(logLine);
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
