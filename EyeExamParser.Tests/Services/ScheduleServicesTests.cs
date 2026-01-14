using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EyeExamParser.DTO;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Xunit;

namespace EyeExamParser.Tests.Services
{
    public class ScheduleServicesTests
    {
        [Fact]
        public async Task GetSchedulesAsync_WhenCacheHasValue_ReturnsCachedAndDoesNotCallHttpOrParser()
        {
            //Arrange
            var handler = new FakeHttpMessageHandler(_ => throw new Exception("HTTP should not be called"));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var parser = Substitute.For<IScheduleParser>();

            var cached = new List<ScheduleDTO>
            {
                new ScheduleDTO { EntryNumber = 1, RegistrationDateAndPlanRef = "cached" }
            }.AsReadOnly();

            cache.Set("parsed_schedule_cache", cached);

            var svc = new ScheduleServices(httpClient, cache, parser);

            //Act
            var result = await svc.GetSchedulesAsync();

            //Assert
            Assert.Same(cached, result);
            Assert.Equal(0, handler.CallCount);
            parser.DidNotReceiveWithAnyArgs().Parse(default!);
        }

        [Fact]
        public async Task GetSchedulesAsync_WhenCacheEmpty_FetchesSchedules_ParsesAndCaches()
        {
            //Arrange
            var raw = new List<RawScheduleDTO>
            {
                new RawScheduleDTO { EntryNumber = "1", EntryDate = "", EntryType = "Schedule", EntryText = new List<string>{ "line" } },
                new RawScheduleDTO { EntryNumber = "2", EntryDate = "", EntryType = "Schedule", EntryText = new List<string>{ "line" } }
            };

            var parsed = new List<ScheduleDTO>
            {
                new ScheduleDTO { EntryNumber = 1, RegistrationDateAndPlanRef = "A" },
                new ScheduleDTO { EntryNumber = 2, RegistrationDateAndPlanRef = "B" }
            };

            var handler = new FakeHttpMessageHandler(req =>
            {
                if (req.RequestUri?.AbsolutePath == "/schedules")
                    return FakeHttpMessageHandler.Json(raw);

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var parser = Substitute.For<IScheduleParser>();
            parser.Parse(Arg.Any<IEnumerable<RawScheduleDTO>>()).Returns(parsed);

            var svc = new ScheduleServices(httpClient, cache, parser);

            //Act
            var result = await svc.GetSchedulesAsync();

            //Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].EntryNumber);
            Assert.Equal(2, result[1].EntryNumber);

            Assert.Equal(1, handler.CallCount);
            parser.Received(1).Parse(Arg.Any<IEnumerable<RawScheduleDTO>>());

            // verify cached
            Assert.True(cache.TryGetValue("parsed_schedule_cache", out IReadOnlyList<ScheduleDTO> cached));
            Assert.Equal(2, cached.Count);
            Assert.Equal("A", cached[0].RegistrationDateAndPlanRef);
        }

        [Fact]
        public async Task VerifyAgainstExternalResultsAsync_WhenCacheEmpty_ReturnsNoCacheIsEmpty()
        {
            //Arrange
            var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(new List<ScheduleDTO>()));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var parser = Substitute.For<IScheduleParser>();

            var svc = new ScheduleServices(httpClient, cache, parser);

            //Act
            var result = await svc.VerifyAgainstExternalResultsAsync();

            //Assert
            Assert.Equal("NO - cache is empty", result);
            Assert.Equal(0, handler.CallCount); // does not call /results if cache missing
        }

        [Fact]
        public async Task VerifyAgainstExternalResultsAsync_WhenMatches_ReturnsYES()
        {
            //Arrange
            var cached = new List<ScheduleDTO>
            {
                new ScheduleDTO
                {
                    EntryNumber = 1,
                    RegistrationDateAndPlanRef = "09.07.2009   Edged   and numbered 2 in  blue (part of)",
                    PropertyDescription = "Endeavour House, 47  Cuba   Street, London",
                    DateOfLeaseAndTerm = "06.07.2009 125 years from  1.1.2009",
                    LesseesTitle = "EGL557357",
                    Notes = null
                }
            }.AsReadOnly();

            // external is logically same but with different whitespace
            var external = new List<ScheduleDTO>
            {
                new ScheduleDTO
                {
                    EntryNumber = 1,
                    RegistrationDateAndPlanRef = "09.07.2009 Edged and numbered 2 in blue (part of)",
                    PropertyDescription = "Endeavour House, 47 Cuba Street, London",
                    DateOfLeaseAndTerm = "06.07.2009 125 years from 1.1.2009",
                    LesseesTitle = "EGL557357",
                    Notes = new List<string>()
                }
            };

            var handler = new FakeHttpMessageHandler(req =>
            {
                if (req.RequestUri?.AbsolutePath == "/results")
                    return FakeHttpMessageHandler.Json(external);

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

            using var cache = new MemoryCache(new MemoryCacheOptions());
            cache.Set("parsed_schedule_cache", cached);

            var parser = Substitute.For<IScheduleParser>();
            var svc = new ScheduleServices(httpClient, cache, parser);

            //Act
            var result = await svc.VerifyAgainstExternalResultsAsync();

            //Assert
            Assert.Equal("YES", result);
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task VerifyAgainstExternalResultsAsync_WhenDiffers_ReturnsNOWithDiffDetails()
        {
            //Arrange
            var cached = new List<ScheduleDTO>
            {
                new ScheduleDTO
                {
                    EntryNumber = 1,
                    RegistrationDateAndPlanRef = "AAA",
                    PropertyDescription = "BBB",
                    DateOfLeaseAndTerm = "CCC",
                    LesseesTitle = "DDD",
                    Notes = new List<string> { "NOTE 1: ours" }
                }
            }.AsReadOnly();

            var external = new List<ScheduleDTO>
            {
                new ScheduleDTO
                {
                    EntryNumber = 1,
                    RegistrationDateAndPlanRef = "AAA",
                    PropertyDescription = "DIFFERENT",
                    DateOfLeaseAndTerm = "CCC",
                    LesseesTitle = "DDD",
                    Notes = new List<string> { "NOTE 1: expected" }
                }
            };

            var handler = new FakeHttpMessageHandler(req =>
            {
                if (req.RequestUri?.AbsolutePath == "/results")
                    return FakeHttpMessageHandler.Json(external);

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

            using var cache = new MemoryCache(new MemoryCacheOptions());
            cache.Set("parsed_schedule_cache", cached);

            var parser = Substitute.For<IScheduleParser>();
            var svc = new ScheduleServices(httpClient, cache, parser);

            //Act
            var result = await svc.VerifyAgainstExternalResultsAsync();

            //Assert
            Assert.StartsWith("NO", result);
            Assert.Contains("Entry 1 - PropertyDescription differs:", result);
            Assert.Contains("Entry 1 - Note[0] differs:", result);
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task VerifyAgainstExternalResultsAsync_WhenMissingAndExtraEntries_ReturnsNOWithMissingAndExtra()
        {
            //Arrange
            var cached = new List<ScheduleDTO>
            {
                new ScheduleDTO { EntryNumber = 1 },
                new ScheduleDTO { EntryNumber = 2 }
            }.AsReadOnly();

            var external = new List<ScheduleDTO>
            {
                new ScheduleDTO { EntryNumber = 2 }, // common
                new ScheduleDTO { EntryNumber = 3 }  // missing from ours
            };

            var handler = new FakeHttpMessageHandler(req =>
            {
                if (req.RequestUri?.AbsolutePath == "/results")
                    return FakeHttpMessageHandler.Json(external);

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

            using var cache = new MemoryCache(new MemoryCacheOptions());
            cache.Set("parsed_schedule_cache", cached);

            var parser = Substitute.For<IScheduleParser>();
            var svc = new ScheduleServices(httpClient, cache, parser);

            //Act
            var result = await svc.VerifyAgainstExternalResultsAsync();

            //Assert
            Assert.StartsWith("NO", result);
            Assert.Contains("Missing EntryNumber 3", result);
            Assert.Contains("Extra EntryNumber 1", result);
            Assert.Equal(1, handler.CallCount);
        }

        #region Test Helpers
        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

            public int CallCount { get; private set; }

            public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                _responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(_responder(request));
            }

            public static HttpResponseMessage Json<T>(T value)
            {
                var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        }
        #endregion
    }
}
