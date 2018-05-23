using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using LogParser.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogParser.Tests
{
    [TestClass]
    public class LogParserTests
    {
        private LogItemsParser target;
        private List<Pattern> patterns;

        [TestInitialize]
        public void Setup()
        {
            this.target = new LogItemsParser();
            this.patterns = new List<Pattern>();
        }

        [TestMethod]
        public void ParseLog_LineMatches_AddsLogItem()
        {
            var log = "Day 1";
            patterns.Add(new Pattern("Day 1"));

            var items = this.target.ParseLog(log, patterns);
            
            items[0].Header.Should().Be("Day 1");
        }

        [TestMethod]
        public void ParseLog_LineAfterMatched_AddsToPreviousLogItem()
        {
            var log = "Day 1\nTest";
            patterns.Add(new Pattern("Day 1"));

            var items = this.target.ParseLog(log, patterns);

            items[0].Lines.Should().Contain("Test");
        }

        [TestMethod]
        public void ParseLog_LineBeforeAnyMatched_Ignore()
        {
            var log = "Test\nDay 1";
            patterns.Add(new Pattern("Day 1"));

            var items = this.target.ParseLog(log, patterns);

            items.Should().NotContain(i => i.Header == "Test" && i.Lines.Contains("Test"));
        }

        [TestMethod]
        public void ParseLog_NestedPattern_AddsNestedLog()
        {
            var log = "Day 1\nTest";
            patterns.Add(new Pattern("Day 1"));
            patterns[0].Children.Add(new Pattern("Test"));

            var items = this.target.ParseLog(log, patterns);

            items[0].Children[0].Header.Should().Be("Test");
        }
    }

    [TestClass]
    public class PatternParserTests
    {
        private PatternParser target;

        [TestInitialize]
        public void Setup()
        {
            this.target = new PatternParser();
        }

        [TestMethod]
        public void GetPatterns_WhenInvoked_Parse()
        {
            var data = "Day 1";

            var patterns = this.target.GetPatterns(data);

            patterns.Should().Contain(p => p.Value == "Day 1");
        }

        [TestMethod]
        public void GetPatterns_WhenMultiline_ParseAllLines()
        {
            var data = "Day 1\nDay 2";

            var patterns = this.target.GetPatterns(data);

            patterns.Should().Contain(p => p.Value == "Day 2");
        }

        [TestMethod]
        public void GetPatterns_WhenInvoked_ParseChildItems()
        {
            var data = "Day 1\n\r\tConsidering goal";

            var patterns = this.target.GetPatterns(data);

            patterns.First().Children.Should().Contain(p => p.Value == "Considering goal");
        }

        [TestMethod]
        public void GetPatterns_WhenItemHasAFewChildren_ParseChildItems()
        {
            var data = "Day 1\n\tConsidering goal 1\n\tConsidering goal 2";

            var patterns = this.target.GetPatterns(data);

            patterns.First().Children.Should().Contain(p => p.Value == "Considering goal 2");
        }

        [TestMethod]
        public void GetPatterns_WhenIndentDecreases_PutsPatternToCorrectAncestor()
        {
            var data = "Day 1\n\tConsidering goal 1\n\tConsidering goal 2\nDay 2";

            var patterns = this.target.GetPatterns(data);

            patterns.Should().Contain(p => p.Value == "Day 2");
        }
    }
}
