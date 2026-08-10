using System.Text.Json;
using DataProcessor.Core.Consumers;
using DataProcessor.Data;
using FluentAssertions;
using MassTransit;
using MetersApp.Shared.Enums;
using MetersApp.Shared.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataProcessor.Core.Tests.Consumers;

public class SensorDataBatchConsumerTests
{
    private readonly DataProcessorDbContext _dbContext;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly SensorDataBatchConsumer _consumer;

    public SensorDataBatchConsumerTests()
    {
        var options = new DbContextOptionsBuilder<DataProcessorDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DataProcessorDbContext(options);
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _consumer = new SensorDataBatchConsumer(
            _dbContext,
            NullLogger<SensorDataBatchConsumer>.Instance,
            _mockPublishEndpoint.Object);
    }

    [Fact]
    public async Task Consume_WithAirQualityData_ShouldSaveToDatabase()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var payloadDoc = JsonDocument.Parse("""
                                            {
                                                "co2": 450,
                                                "pm25": 35,
                                                "humidity": 60
                                            }
                                            """);
        var payload = payloadDoc.RootElement.Clone();

        var batch = new ProcessSensorDataBatch
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.AirQuality,
                    LocationType = LocationType.LivingRoom,
                    Timestamp = timestamp,
                    Payload = payload,
                },
            },
        };

        var context = CreateConsumeContext(batch);

        // Act
        await _consumer.Consume(context);

        // Assert
        var readings = await _dbContext.AirQualityReadings.ToListAsync(TestContext.Current.CancellationToken);
        readings.Should().HaveCount(1);
        readings[0].Co2.Should().Be(450);
        readings[0].Pm25.Should().Be(35);
        readings[0].Humidity.Should().Be(60);
        readings[0].LocationId.Should().Be(LocationType.LivingRoom);
        readings[0].Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public async Task Consume_WithEnergyData_ShouldSaveToDatabase()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var payloadDoc = JsonDocument.Parse("""
            {
                "energy": 1250.75
            }
            """);
        var payload = payloadDoc.RootElement.Clone();

        var batch = new ProcessSensorDataBatch
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.Energy,
                    LocationType = LocationType.Kitchen,
                    Timestamp = timestamp,
                    Payload = payload,
                },
            },
        };

        var context = CreateConsumeContext(batch);

        // Act
        await _consumer.Consume(context);

        // Assert
        var readings = await _dbContext.EnergyReadings.ToListAsync(TestContext.Current.CancellationToken);
        readings.Should().HaveCount(1);
        readings[0].Energy.Should().Be(1250.75f);
        readings[0].LocationId.Should().Be(LocationType.Kitchen);
        readings[0].Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public async Task Consume_WithMotionData_ShouldSaveToDatabase()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var payloadDoc = JsonDocument.Parse("""
            {
                "motionDetected": true
            }
            """);
        var payload = payloadDoc.RootElement.Clone();

        var batch = new ProcessSensorDataBatch
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.Motion,
                    LocationType = LocationType.Office,
                    Timestamp = timestamp,
                    Payload = payload,
                },
            },
        };

        var context = CreateConsumeContext(batch);

        // Act
        await _consumer.Consume(context);

        // Assert
        var readings = await _dbContext.MotionReadings.ToListAsync(TestContext.Current.CancellationToken);
        readings.Should().HaveCount(1);
        readings[0].MotionDetected.Should().BeTrue();
        readings[0].LocationId.Should().Be(LocationType.Office);
        readings[0].Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public async Task Consume_WithMixedSensorTypes_ShouldSaveToCorrectDbSets()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        var airQualityPayload = JsonDocument.Parse("""
            {
                "co2": 450,
                "pm25": 35,
                "humidity": 60
            }
            """).RootElement.Clone();

        var energyPayload = JsonDocument.Parse("""
            {
                "energy": 1000
            }
            """).RootElement.Clone();

        var motionPayload = JsonDocument.Parse("""
            {
                "motionDetected": false
            }
            """).RootElement.Clone();

        var batch = new ProcessSensorDataBatch
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.AirQuality,
                    LocationType = LocationType.LivingRoom,
                    Timestamp = timestamp,
                    Payload = airQualityPayload,
                },
                new()
                {
                    SensorType = SensorType.Energy,
                    LocationType = LocationType.Kitchen,
                    Timestamp = timestamp,
                    Payload = energyPayload,
                },
                new()
                {
                    SensorType = SensorType.Motion,
                    LocationType = LocationType.Office,
                    Timestamp = timestamp,
                    Payload = motionPayload,
                },
            },
        };

        var context = CreateConsumeContext(batch);

        // Act
        await _consumer.Consume(context);

        // Assert
        var airQualityReadings = await _dbContext.AirQualityReadings.ToListAsync(TestContext.Current.CancellationToken);
        var energyReadings = await _dbContext.EnergyReadings.ToListAsync(TestContext.Current.CancellationToken);
        var motionReadings = await _dbContext.MotionReadings.ToListAsync(TestContext.Current.CancellationToken);

        airQualityReadings.Should().HaveCount(1);
        energyReadings.Should().HaveCount(1);
        motionReadings.Should().HaveCount(1);

        airQualityReadings[0].LocationId.Should().Be(LocationType.LivingRoom);
        energyReadings[0].LocationId.Should().Be(LocationType.Kitchen);
        motionReadings[0].LocationId.Should().Be(LocationType.Office);
    }

    [Fact]
    public async Task Consume_WithUnknownSensorType_ShouldNotThrowAndContinue()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var airQualityPayload = JsonDocument.Parse("""
            {
                "co2": 450,
                "pm25": 35,
                "humidity": 60
            }
            """).RootElement.Clone();

        var unknownPayload = JsonDocument.Parse("""
            {
                "someField": "someValue"
            }
            """).RootElement.Clone();

        var batch = new ProcessSensorDataBatch
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.AirQuality,
                    LocationType = LocationType.LivingRoom,
                    Timestamp = timestamp,
                    Payload = airQualityPayload,
                },
                new()
                {
                    SensorType = SensorType.Unknown,
                    LocationType = LocationType.Kitchen,
                    Timestamp = timestamp,
                    Payload = unknownPayload,
                },
            },
        };

        var context = CreateConsumeContext(batch);

        // Act
        await _consumer.Consume(context);

        // Assert
        var airQualityReadings = await _dbContext.AirQualityReadings.ToListAsync(TestContext.Current.CancellationToken);
        airQualityReadings.Should().HaveCount(1);

        var allEnergyReadings = await _dbContext.EnergyReadings.ToListAsync(TestContext.Current.CancellationToken);
        var allMotionReadings = await _dbContext.MotionReadings.ToListAsync(TestContext.Current.CancellationToken);
        allEnergyReadings.Should().BeEmpty();
        allMotionReadings.Should().BeEmpty();
    }

    [Fact]
    public async Task Consume_WithInvalidPayload_ShouldSkipItem()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        var validPayload = JsonDocument.Parse("""
            {
                "co2": 450,
                "pm25": 35,
                "humidity": 60
            }
            """).RootElement.Clone();

        var invalidPayload = JsonDocument.Parse("""
            {
                "invalidField": "invalidValue"
            }
            """).RootElement.Clone();

        var batch = new ProcessSensorDataBatch
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.AirQuality,
                    LocationType = LocationType.LivingRoom,
                    Timestamp = timestamp,
                    Payload = validPayload,
                },
                new()
                {
                    SensorType = SensorType.AirQuality,
                    LocationType = LocationType.Kitchen,
                    Timestamp = timestamp,
                    Payload = invalidPayload,
                },
            },
        };

        var context = CreateConsumeContext(batch);

        // Act
        await _consumer.Consume(context);

        // Assert
        var readings = await _dbContext.AirQualityReadings.ToListAsync(TestContext.Current.CancellationToken);
        readings.Should().HaveCount(2);

        // First item should be saved correctly
        readings[0].Co2.Should().Be(450);
        readings[0].Pm25.Should().Be(35);
        readings[0].Humidity.Should().Be(60);

        // Second item should be saved with default values (since deserialization succeeds but values are 0)
        readings[1].Co2.Should().Be(0);
        readings[1].Pm25.Should().Be(0);
        readings[1].Humidity.Should().Be(0);
    }

    [Fact]
    public async Task Consume_ShouldPublishNewSensorDataEvent()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var payload = JsonDocument.Parse("""
            {
                "energy": 1000
            }
            """).RootElement.Clone();

        var batch = new ProcessSensorDataBatch
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.Energy,
                    LocationType = LocationType.LivingRoom,
                    Timestamp = timestamp,
                    Payload = payload,
                },
            },
        };

        var context = CreateConsumeContext(batch);

        NewSensorDataEvent? publishedEvent = null;
        _mockPublishEndpoint
            .Setup(x => x.Publish(It.IsAny<NewSensorDataEvent>(), It.IsAny<CancellationToken>()))
            .Callback<NewSensorDataEvent, CancellationToken>((evt, _) => publishedEvent = evt)
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(context);

        // Assert
        _mockPublishEndpoint.Verify(
            x => x.Publish(It.IsAny<NewSensorDataEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        publishedEvent.Should().NotBeNull();
        publishedEvent.Items.Should().HaveCount(1);
        publishedEvent.Items.First().SensorType.Should().Be(SensorType.Energy);
    }

    [Fact]
    public async Task Consume_WithEmptyBatch_ShouldPublishEmptyEvent()
    {
        // Arrange
        var batch = new ProcessSensorDataBatch
        {
            Items = new List<SensorDataItem>(),
        };

        var context = CreateConsumeContext(batch);

        NewSensorDataEvent? publishedEvent = null;
        _mockPublishEndpoint
            .Setup(x => x.Publish(It.IsAny<NewSensorDataEvent>(), It.IsAny<CancellationToken>()))
            .Callback<NewSensorDataEvent, CancellationToken>((evt, _) => publishedEvent = evt)
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(context);

        // Assert
        _mockPublishEndpoint.Verify(
            x => x.Publish(It.IsAny<NewSensorDataEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        publishedEvent.Should().NotBeNull();
        publishedEvent.Items.Should().BeEmpty();

        var airQualityReadings = await _dbContext.AirQualityReadings.ToListAsync(TestContext.Current.CancellationToken);
        var energyReadings = await _dbContext.EnergyReadings.ToListAsync(TestContext.Current.CancellationToken);
        var motionReadings = await _dbContext.MotionReadings.ToListAsync(TestContext.Current.CancellationToken);

        airQualityReadings.Should().BeEmpty();
        energyReadings.Should().BeEmpty();
        motionReadings.Should().BeEmpty();
    }

    [Fact]
    public async Task Consume_ShouldSetLocationAndTimestampOnAllReadings()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var locations = new[] { LocationType.LivingRoom, LocationType.Kitchen, LocationType.Office };
        var items = new List<SensorDataItem>();

        for (int i = 0; i < locations.Length; i++)
        {
            var payload = JsonDocument.Parse($"{{\"co2\": {400 + i}, \"pm25\": {30 + i}, \"humidity\": {50 + i}}}")
                .RootElement.Clone();

            items.Add(new SensorDataItem
            {
                SensorType = SensorType.AirQuality,
                LocationType = locations[i],
                Timestamp = timestamp.AddMinutes(i),
                Payload = payload,
            });
        }

        var batch = new ProcessSensorDataBatch { Items = items };
        var context = CreateConsumeContext(batch);

        // Act
        await _consumer.Consume(context);

        // Assert
        var readings = await _dbContext.AirQualityReadings.ToListAsync(TestContext.Current.CancellationToken);
        readings.Should().HaveCount(3);

        for (int i = 0; i < locations.Length; i++)
        {
            readings[i].LocationId.Should().Be(locations[i]);
            readings[i].Timestamp.Should().Be(timestamp.AddMinutes(i));
        }
    }

    [Fact]
    public async Task Consume_MultipleCalls_ShouldAccumulateReadings()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        var batch1 = new ProcessSensorDataBatch
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.Energy,
                    LocationType = LocationType.LivingRoom,
                    Timestamp = timestamp,
                    Payload = JsonDocument.Parse("{\"energy\": 100}").RootElement.Clone(),
                },
            },
        };

        var batch2 = new ProcessSensorDataBatch
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.Energy,
                    LocationType = LocationType.Kitchen,
                    Timestamp = timestamp.AddMinutes(1),
                    Payload = JsonDocument.Parse("{\"energy\": 200}").RootElement.Clone(),
                },
            },
        };

        // Act
        await _consumer.Consume(CreateConsumeContext(batch1));
        await _consumer.Consume(CreateConsumeContext(batch2));

        // Assert
        var readings = await _dbContext.EnergyReadings.ToListAsync(TestContext.Current.CancellationToken);
        readings.Should().HaveCount(2);
        readings[0].Energy.Should().Be(100f);
        readings[1].Energy.Should().Be(200f);
    }

    [Fact]
    public async Task Consume_WithMultipleItemsOfSameType_ShouldSaveAll()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var items = new List<SensorDataItem>();

        for (int i = 0; i < 5; i++)
        {
            items.Add(new SensorDataItem
            {
                SensorType = SensorType.Motion,
                LocationType = LocationType.Corridor,
                Timestamp = timestamp.AddSeconds(i),
                Payload = JsonDocument.Parse($"{{\"motionDetected\": {(i % 2 == 0).ToString().ToLower()}}}")
                    .RootElement.Clone(),
            });
        }

        var batch = new ProcessSensorDataBatch { Items = items };
        var context = CreateConsumeContext(batch);

        // Act
        await _consumer.Consume(context);

        // Assert
        var readings = await _dbContext.MotionReadings.ToListAsync(TestContext.Current.CancellationToken);
        readings.Should().HaveCount(5);

        for (var i = 0; i < 5; i++)
        {
            readings[i].MotionDetected.Should().Be(i % 2 == 0);
            readings[i].LocationId.Should().Be(LocationType.Corridor);
        }
    }

    private static ConsumeContext<ProcessSensorDataBatch> CreateConsumeContext(ProcessSensorDataBatch message)
    {
        var mockContext = new Mock<ConsumeContext<ProcessSensorDataBatch>>();
        mockContext.Setup(x => x.Message).Returns(message);
        mockContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);
        return mockContext.Object;
    }
}
