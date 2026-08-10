using System.Text.Json;
using FluentAssertions;
using MassTransit;
using MetersApp.Shared.Enums;
using MetersApp.Shared.Messages;
using Microsoft.Extensions.Logging;
using Moq;
using Notifications.Core.Consumers;
using Notifications.Core.Dto;
using Notifications.Core.Interfaces;

namespace Notifications.Core.Tests.Consumers;

public class NewSensorDataEventConsumerTests
{
    private readonly Mock<ISensorBroadcaster> _mockBroadcaster;
    private readonly Mock<ILogger<NewSensorDataEventConsumer>> _mockLogger;
    private readonly NewSensorDataEventConsumer _consumer;

    public NewSensorDataEventConsumerTests()
    {
        _mockBroadcaster = new Mock<ISensorBroadcaster>();
        _mockLogger = new Mock<ILogger<NewSensorDataEventConsumer>>();
        _consumer = new NewSensorDataEventConsumer(_mockLogger.Object, _mockBroadcaster.Object);
    }

    [Fact]
    public async Task Consume_WithSingleItem_ShouldCallBroadcastAsync()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var payload = JsonDocument.Parse("""{"value": 22.5}""").RootElement.Clone();

        var message = new NewSensorDataEvent
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

        var context = CreateConsumeContext(message, TestContext.Current.CancellationToken);

        SensorDataDto? capturedDto = null;
        _mockBroadcaster
            .Setup(x => x.BroadcastAsync(It.IsAny<SensorDataDto>(), It.IsAny<CancellationToken>()))
            .Callback<SensorDataDto, CancellationToken>((dto, _) => capturedDto = dto)
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(context);

        // Assert
        _mockBroadcaster.Verify(
            x => x.BroadcastAsync(It.IsAny<SensorDataDto>(), It.IsAny<CancellationToken>()),
            Times.Once);

        capturedDto.Should().NotBeNull();
        capturedDto.Items.Should().HaveCount(1);

        var item = capturedDto.Items.First();
        item.SensorType.Should().Be(SensorType.Energy);
        item.LocationType.Should().Be(LocationType.LivingRoom);
        item.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public async Task Consume_WithMultipleItems_ShouldBroadcastAllItems()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        var message = new NewSensorDataEvent
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.Energy,
                    LocationType = LocationType.LivingRoom,
                    Timestamp = timestamp,
                    Payload = JsonDocument.Parse("""{"energy": 100}""").RootElement.Clone(),
                },
                new()
                {
                    SensorType = SensorType.AirQuality,
                    LocationType = LocationType.Kitchen,
                    Timestamp = timestamp.AddMinutes(1),
                    Payload = JsonDocument.Parse("""{"co2": 450}""").RootElement.Clone(),
                },
                new()
                {
                    SensorType = SensorType.Motion,
                    LocationType = LocationType.Office,
                    Timestamp = timestamp.AddMinutes(2),
                    Payload = JsonDocument.Parse("""{"motionDetected": true}""").RootElement.Clone(),
                },
            },
        };

        var context = CreateConsumeContext(message, TestContext.Current.CancellationToken);

        SensorDataDto? capturedDto = null;
        _mockBroadcaster
            .Setup(x => x.BroadcastAsync(It.IsAny<SensorDataDto>(), It.IsAny<CancellationToken>()))
            .Callback<SensorDataDto, CancellationToken>((dto, _) => capturedDto = dto)
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(context);

        // Assert
        capturedDto.Should().NotBeNull();
        capturedDto.Items.Should().HaveCount(3);

        var items = capturedDto.Items.ToList();
        items[0].SensorType.Should().Be(SensorType.Energy);
        items[0].LocationType.Should().Be(LocationType.LivingRoom);

        items[1].SensorType.Should().Be(SensorType.AirQuality);
        items[1].LocationType.Should().Be(LocationType.Kitchen);

        items[2].SensorType.Should().Be(SensorType.Motion);
        items[2].LocationType.Should().Be(LocationType.Office);
    }

    [Fact]
    public async Task Consume_WithEmptyItems_ShouldBroadcastEmptyDto()
    {
        // Arrange
        var message = new NewSensorDataEvent
        {
            Items = new List<SensorDataItem>(),
        };

        var context = CreateConsumeContext(message, TestContext.Current.CancellationToken);

        SensorDataDto? capturedDto = null;
        _mockBroadcaster
            .Setup(x => x.BroadcastAsync(It.IsAny<SensorDataDto>(), It.IsAny<CancellationToken>()))
            .Callback<SensorDataDto, CancellationToken>((dto, _) => capturedDto = dto)
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(context);

        // Assert
        _mockBroadcaster.Verify(
            x => x.BroadcastAsync(It.IsAny<SensorDataDto>(), It.IsAny<CancellationToken>()),
            Times.Once);

        capturedDto.Should().NotBeNull();
        capturedDto.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Consume_WithExceptionInBroadcaster_ShouldLogError()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var payload = JsonDocument.Parse("""{"value": 22.5}""").RootElement.Clone();

        var message = new NewSensorDataEvent
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

        var context = CreateConsumeContext(message, TestContext.Current.CancellationToken);

        var expectedException = new InvalidOperationException("Broadcast failed");
        _mockBroadcaster
            .Setup(x => x.BroadcastAsync(It.IsAny<SensorDataDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        await _consumer.Consume(context);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => (v.ToString() ?? string.Empty).Contains("An exception occurred consuming the message")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldMapMessagePropertiesCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var payloadJson = """
            {
                "temperature": 23.5,
                "humidity": 65,
                "battery": 85
            }
            """;
        var payload = JsonDocument.Parse(payloadJson).RootElement.Clone();

        var message = new NewSensorDataEvent
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.AirQuality,
                    LocationType = LocationType.Bedroom,
                    Timestamp = timestamp,
                    Payload = payload,
                },
            },
        };

        var context = CreateConsumeContext(message, TestContext.Current.CancellationToken);

        SensorDataDto? capturedDto = null;
        _mockBroadcaster
            .Setup(x => x.BroadcastAsync(It.IsAny<SensorDataDto>(), It.IsAny<CancellationToken>()))
            .Callback<SensorDataDto, CancellationToken>((dto, _) => capturedDto = dto)
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(context);

        // Assert
        capturedDto.Should().NotBeNull();
        var item = capturedDto.Items.First();

        item.SensorType.Should().Be(SensorType.AirQuality);
        item.LocationType.Should().Be(LocationType.Bedroom);
        item.Timestamp.Should().Be(timestamp);

        // Verify payload is preserved
        item.Payload.GetProperty("temperature").GetDouble().Should().Be(23.5);
        item.Payload.GetProperty("humidity").GetInt32().Should().Be(65);
        item.Payload.GetProperty("battery").GetInt32().Should().Be(85);
    }

    [Fact]
    public async Task Consume_ShouldPassCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var message = new NewSensorDataEvent
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.Energy,
                    LocationType = LocationType.LivingRoom,
                    Timestamp = DateTime.UtcNow,
                    Payload = JsonDocument.Parse("""{"value": 100}""").RootElement.Clone(),
                },
            },
        };

        var context = CreateConsumeContext(message, token);

        CancellationToken? capturedToken = null;
        _mockBroadcaster
            .Setup(x => x.BroadcastAsync(It.IsAny<SensorDataDto>(), It.IsAny<CancellationToken>()))
            .Callback<SensorDataDto, CancellationToken>((_, ct) => capturedToken = ct)
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(context);

        // Assert
        capturedToken.Should().Be(token);
    }

    [Fact]
    public async Task Consume_WithUnknownSensorType_ShouldMapCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var payload = JsonDocument.Parse("""{"value": 0}""").RootElement.Clone();

        var message = new NewSensorDataEvent
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.Unknown,
                    LocationType = LocationType.Corridor,
                    Timestamp = timestamp,
                    Payload = payload,
                },
            },
        };

        var context = CreateConsumeContext(message, TestContext.Current.CancellationToken);

        SensorDataDto? capturedDto = null;
        _mockBroadcaster
            .Setup(x => x.BroadcastAsync(It.IsAny<SensorDataDto>(), It.IsAny<CancellationToken>()))
            .Callback<SensorDataDto, CancellationToken>((dto, _) => capturedDto = dto)
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(context);

        // Assert
        capturedDto.Should().NotBeNull();
        capturedDto.Items.Should().HaveCount(1);
        capturedDto.Items.First().SensorType.Should().Be(SensorType.Unknown);
    }

    [Fact]
    public async Task Consume_MultipleCalls_ShouldBroadcastEachMessage()
    {
        // Arrange
        var message1 = new NewSensorDataEvent
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.Energy,
                    LocationType = LocationType.LivingRoom,
                    Timestamp = DateTime.UtcNow,
                    Payload = JsonDocument.Parse("""{"value": 100}""").RootElement.Clone(),
                },
            },
        };

        var message2 = new NewSensorDataEvent
        {
            Items = new List<SensorDataItem>
            {
                new()
                {
                    SensorType = SensorType.Motion,
                    LocationType = LocationType.Kitchen,
                    Timestamp = DateTime.UtcNow,
                    Payload = JsonDocument.Parse("""{"detected": true}""").RootElement.Clone(),
                },
            },
        };

        _mockBroadcaster
            .Setup(x => x.BroadcastAsync(It.IsAny<SensorDataDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(CreateConsumeContext(message1, TestContext.Current.CancellationToken));
        await _consumer.Consume(CreateConsumeContext(message2, TestContext.Current.CancellationToken));

        // Assert
        _mockBroadcaster.Verify(
            x => x.BroadcastAsync(It.IsAny<SensorDataDto>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private static ConsumeContext<NewSensorDataEvent> CreateConsumeContext(
        NewSensorDataEvent message,
        CancellationToken cancellationToken = default)
    {
        var mockContext = new Mock<ConsumeContext<NewSensorDataEvent>>();
        mockContext.Setup(x => x.Message).Returns(message);
        mockContext.Setup(x => x.CancellationToken).Returns(cancellationToken);
        return mockContext.Object;
    }
}
