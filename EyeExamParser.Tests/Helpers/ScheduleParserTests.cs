using System;
using System.Collections.Generic;
using System.Linq;
using EyeExamParser.DTO;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EyeExamParser.Tests.Parsers
{
    public class ScheduleParserTests
    {
        [Fact]
        public void Parse_RawIsNull_ReturnsEmptyList()
        {
            //Arrange
            var logger = Substitute.For<ILogger<ScheduleParser>>();
            var parser = new ScheduleParser(logger);

            //Act
            var result = parser.Parse(null);

            //Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void Parse_ValidDataset_ParsesAllEntriesCorrectly()
        {
            //Arrange
            var logger = Substitute.For<ILogger<ScheduleParser>>();
            var parser = new ScheduleParser(logger);

            var raw = BuildDataset();

            //Act
            var result = parser.Parse(raw).ToList();

            //Assert
            Assert.Equal(5, result.Count);

            // entry 1
            var e1 = result.Single(x => x.EntryNumber == 1);
            Assert.Null(e1.EntryDate);
            Assert.Equal("09.07.2009 Edged and numbered 2 in blue (part of)", e1.RegistrationDateAndPlanRef);
            Assert.Equal("Endeavour House, 47 Cuba Street, London", e1.PropertyDescription);
            Assert.Equal("06.07.2009 125 years from 1.1.2009", e1.DateOfLeaseAndTerm);
            Assert.Equal("EGL557357", e1.LesseesTitle);
            Assert.Null(e1.Notes);

            // entry 2
            var e2 = result.Single(x => x.EntryNumber == 2);
            Assert.Null(e2.EntryDate);
            Assert.Equal("15.11.2018 Edged and numbered 2 in blue (part of)", e2.RegistrationDateAndPlanRef);
            Assert.Equal("Ground Floor Premises", e2.PropertyDescription);
            Assert.Equal("10.10.2018 from 10 October 2018 to and including 19 April 2028", e2.DateOfLeaseAndTerm);
            Assert.Equal("TGL513556", e2.LesseesTitle);
            Assert.Null(e2.Notes);

            // entry 3
            var e3 = result.Single(x => x.EntryNumber == 3);
            Assert.Null(e3.EntryDate);
            Assert.Equal("16.08.2013", e3.RegistrationDateAndPlanRef);
            Assert.Equal("21 Sheen Road (Ground floor shop)", e3.PropertyDescription);
            Assert.Equal("06.08.2013 Beginning on and including 6.8.2013 and ending on and including 6.8.2023", e3.DateOfLeaseAndTerm);
            Assert.Equal("TGL383606", e3.LesseesTitle);
            Assert.Null(e3.Notes);

            // entry 4
            var e4 = result.Single(x => x.EntryNumber == 4);
            Assert.Null(e4.EntryDate);
            Assert.Equal("24.07.1989 Edged and numbered 19 (Part of) in brown", e4.RegistrationDateAndPlanRef);
            Assert.Equal("17 Ashworth Close (Ground and First Floor Flat)", e4.PropertyDescription);
            Assert.Equal("01.06.1989 125 years from 1.6.1989", e4.DateOfLeaseAndTerm);
            Assert.Equal("TGL24029", e4.LesseesTitle);
            Assert.NotNull(e4.Notes);
            Assert.Equal(3, e4.Notes!.Count);
            Assert.StartsWith("NOTE 1:", e4.Notes[0]);

            // entry 5
            var e5 = result.Single(x => x.EntryNumber == 5);
            Assert.Null(e5.EntryDate);
            Assert.Equal("19.09.1989 Edged and numbered 25 (Part of) in brown", e5.RegistrationDateAndPlanRef);
            Assert.Equal("12 Harbord Close (Ground and First Floor Flat)", e5.PropertyDescription);
            Assert.Equal("01.09.1989 125 years from 1.9.1989", e5.DateOfLeaseAndTerm);
            Assert.Equal("TGL27196", e5.LesseesTitle);
            Assert.NotNull(e5.Notes);
            Assert.Single(e5.Notes!);
            Assert.StartsWith("NOTE:", e5.Notes![0]);

            // sanity: no warnings logged on happy path
            logger.DidNotReceiveWithAnyArgs()
                .Log(default, default, default!, default!, default!);
        }

        [Fact]
        public void Parse_WhenSingleEntryFails_ContinuesParsingAndLogsWarning()
        {
            //Arrange
            var logger = Substitute.For<ILogger<ScheduleParser>>();
            var parser = new ScheduleParser(logger);

            // Make one entry invalid (EntryNumber cannot be parsed)
            var raw = BuildDataset().ToList();
            raw.Insert(2, new RawScheduleDTO
            {
                EntryNumber = "X",
                EntryDate = "",
                EntryType = "Schedule of Notices of Leases",
                EntryText = new List<string> { "bad" }
            });

            //Act
            var result = parser.Parse(raw).ToList();

            //Assert
            // the invalid one should be skipped, the valid 5 should still be present
            Assert.Equal(5, result.Count);
            Assert.Contains(result, x => x.EntryNumber == 1);
            Assert.Contains(result, x => x.EntryNumber == 2);
            Assert.Contains(result, x => x.EntryNumber == 3);
            Assert.Contains(result, x => x.EntryNumber == 4);
            Assert.Contains(result, x => x.EntryNumber == 5);

            // verify a warning was logged at least once
            logger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
        }

        private static IEnumerable<RawScheduleDTO> BuildDataset()
        {
            return new List<RawScheduleDTO>
            {
                new RawScheduleDTO
                {
                    EntryNumber = "1",
                    EntryDate = "",
                    EntryType = "Schedule of Notices of Leases",
                    EntryText = new List<string>
                    {
                        "09.07.2009      Endeavour House, 47 Cuba      06.07.2009      EGL557357  ",
                        "Edged and       Street, London                125 years from             ",
                        "numbered 2 in                                 1.1.2009                   ",
                        "blue (part of)"
                    }
                },
                new RawScheduleDTO
                {
                    EntryNumber = "2",
                    EntryDate = "",
                    EntryType = "Schedule of Notices of Leases",
                    EntryText = new List<string>
                    {
                        "15.11.2018      Ground Floor Premises         10.10.2018      TGL513556  ",
                        "Edged and                                     from 10                    ",
                        "numbered 2 in                                 October 2018               ",
                        "blue (part of)                                to and                     ",
                        "including 19               ",
                        "April 2028"
                    }
                },
                new RawScheduleDTO
                {
                    EntryNumber = "3",
                    EntryDate = "",
                    EntryType = "Schedule of Notices of Leases",
                    EntryText = new List<string>
                    {
                        "16.08.2013      21 Sheen Road (Ground floor   06.08.2013      TGL383606  ",
                        "shop)                         Beginning on               ",
                        "and including              ",
                        "6.8.2013 and               ",
                        "ending on and              ",
                        "including                  ",
                        "6.8.2023"
                    }
                },
                new RawScheduleDTO
                {
                    EntryNumber = "4",
                    EntryDate = "",
                    EntryType = "Schedule of Notices of Leases",
                    EntryText = new List<string>
                    {
                        "24.07.1989      17 Ashworth Close (Ground     01.06.1989      TGL24029   ",
                        "Edged and       and First Floor Flat)         125 years from             ",
                        "numbered 19                                   1.6.1989                   ",
                        "(Part of) in                                                             ",
                        "brown                                                                    ",
                        "NOTE 1: A Deed of Rectification dated 7 September 1992 made between (1) Orbit Housing Association and (2) John Joseph McMahon Nellie Helen McMahon and John George McMahon is supplemental to the Lease dated 1 June 1989 of 17 Ashworth Close referred to above. The lease actually comprises the second floor flat numbered 24 (Part of) on the filed plan. (Copy Deed filed under TGL24029)",
                        "NOTE 2: By a Deed dated 23 May 1996 made between (1) Orbit Housing Association (2) John Joseph McMahon Nellie Helen McMahon and John George McMahon and (3) Britannia Building Society the terms of the lease were varied. (Copy Deed filed under TGL24029).",
                        "NOTE 3: A Deed dated 13 February 1997 made between (1) Orbit Housing Association (2) John Joseph McMahon and others and (3) Britannia Building Society is supplemental to the lease. It substitutes a new plan for the original lease plan. (Copy Deed filed under TGL24029)"
                    }
                },
                new RawScheduleDTO
                {
                    EntryNumber = "5",
                    EntryDate = "",
                    EntryType = "Schedule of Notices of Leases",
                    EntryText = new List<string>
                    {
                        "19.09.1989      12 Harbord Close (Ground      01.09.1989      TGL27196   ",
                        "Edged and       and First Floor Flat)         125 years from             ",
                        "numbered 25                                   1.9.1989                   ",
                        "(Part of) in                                                             ",
                        "brown                                                                    ",
                        "NOTE: By a Deed dated 20 July 1995 made between (1) Orbit Housing Association and (2) Clifford Ronald Mitchell the terms of the Lease were varied.  (Copy Deed filed under TGL27169)"
                    }
                }
            };
        }
    }
}
