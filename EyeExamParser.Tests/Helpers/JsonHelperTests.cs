using EyeExamParser.Helpers;
using System;
using System.Collections.Generic;
using Xunit;

namespace EyeExamParser.Tests.Helpers
{
    public class JsonHelperTests
    {
        [Fact]
        public void Normalize_String_Null_ReturnsEmptyString()
        {
            //Arrange
            string? input = null;

            //Act
            var result = JsonHelper.Normalize(input);

            //Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Normalize_String_Empty_ReturnsEmptyString()
        {
            //Arrange
            string input = string.Empty;

            //Act
            var result = JsonHelper.Normalize(input);

            //Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Normalize_String_CollapsesMultipleSpaces_ToSingleSpaces()
        {
            //Arrange
            string input = "Hello   world    from   parser";

            //Act
            var result = JsonHelper.Normalize(input);

            //Assert
            Assert.Equal("Hello world from parser", result);
        }

        [Fact]
        public void Normalize_String_TrimsLeadingAndTrailingSpaces()
        {
            //Arrange
            string input = "   Hello world   ";

            //Act
            var result = JsonHelper.Normalize(input);

            //Assert
            Assert.Equal("Hello world", result);
        }

        [Fact]
        public void Normalize_String_RemovesEmptyEntries_WhenSplitting()
        {
            //Arrange
            string input = "A   B  C";

            //Act
            var result = JsonHelper.Normalize(input);

            //Assert
            Assert.Equal("A B C", result);
        }

        [Fact]
        public void Normalize_Parts_NullOrWhitespaceEntries_AreIgnored()
        {
            //Arrange
            var parts = new List<string>
            {
                "Hello",
                "",
                "   ",
                "\t",
                "world",
                null // allowed at runtime because List<string> can contain null; method checks IsNullOrWhiteSpace
            };

            //Act
            var result = JsonHelper.Normalize(parts!);

            //Assert
            Assert.Equal("Hello world", result);
        }

        [Fact]
        public void Normalize_Parts_CollapsesInternalWhitespace_ToSingleSpaces()
        {
            //Arrange
            var parts = new List<string>
            {
                "Hello",
                "world",
                "from\t\tparser",
                "with   extra   spaces"
            };

            //Act
            var result = JsonHelper.Normalize(parts);

            //Assert
            Assert.Equal("Hello world from parser with extra spaces", result);
        }

        [Fact]
        public void Normalize_Parts_TrimsFinalResult()
        {
            //Arrange
            var parts = new List<string>
            {
                "  Hello  ",
                "   world   "
            };

            //Act
            var result = JsonHelper.Normalize(parts);

            //Assert
            Assert.Equal("Hello world", result);
        }

        [Fact]
        public void Normalize_Parts_AllWhitespace_ReturnsEmptyString()
        {
            //Arrange
            var parts = new List<string>
            {
                "",
                "   ",
                "\t",
                "\n"
            };

            //Act
            var result = JsonHelper.Normalize(parts);

            //Assert
            Assert.Equal(string.Empty, result);
        }
    }
}
