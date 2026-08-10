using FluentAssertions;
using GraphQLServer.Core.Dto;
using GraphQLServer.Core.Services;
using GraphQLServer.Data;
using GraphQLServer.Data.Entities;
using MetersApp.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace GraphQLServer.Core.Tests.Services;

public class MotionServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly MotionService _service;

    public MotionServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _service = new MotionService(_dbContext);
    }

    [Fact]
    public async Task GetReadings_ShouldReturnQueryable()
    {
        // Act
        var result = await _service.GetReadings().ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IEnumerable<MotionReadingDto>>();
    }

    [Fact]
    public async Task GetReadings_ShouldReturnAllReadings()
    {
        // Arrange
        var readings = new List<MotionReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Office,
                Timestamp = DateTime.UtcNow,
                MotionDetected = true,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Kitchen,
                Timestamp = DateTime.UtcNow.AddMinutes(-1),
                MotionDetected = false,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.LivingRoom,
                Timestamp = DateTime.UtcNow.AddMinutes(-2),
                MotionDetected = true,
            },
        };

        await _dbContext.MotionReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings().ToList();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetReadings_ShouldProjectCorrectFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var reading = new MotionReading
        {
            Id = id,
            LocationId = LocationType.Bedroom,
            Timestamp = timestamp,
            MotionDetected = true,
        };

        await _dbContext.MotionReadings.AddAsync(reading, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings().First();

        // Assert
        result.Id.Should().Be(id);
        result.LocationId.Should().Be(LocationType.Bedroom);
        result.Timestamp.Should().Be(timestamp);
        result.MotionDetected.Should().BeTrue();
    }

    [Fact]
    public async Task GetReadings_ShouldReturnEmpty_WhenNoData()
    {
        // Act
        var result = await _service.GetReadings().ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReadings_ShouldSupportFiltering()
    {
        // Arrange
        var readings = new List<MotionReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Office,
                Timestamp = DateTime.UtcNow,
                MotionDetected = true,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Kitchen,
                Timestamp = DateTime.UtcNow,
                MotionDetected = false,
            },
        };

        await _dbContext.MotionReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings()
            .Where(x => x.MotionDetected)
            .ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].LocationId.Should().Be(LocationType.Office);
    }

    [Fact]
    public async Task GetReadings_ShouldSupportOrdering()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var readings = new List<MotionReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Office,
                Timestamp = baseTime,
                MotionDetected = true,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Kitchen,
                Timestamp = baseTime.AddMinutes(-5),
                MotionDetected = false,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.LivingRoom,
                Timestamp = baseTime.AddMinutes(5),
                MotionDetected = true,
            },
        };

        await _dbContext.MotionReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings()
            .OrderBy(x => x.Timestamp)
            .ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].LocationId.Should().Be(LocationType.Kitchen);
        result[1].LocationId.Should().Be(LocationType.Office);
        result[2].LocationId.Should().Be(LocationType.LivingRoom);
    }

    [Fact]
    public async Task GetReadings_ShouldSupportPagination()
    {
        // Arrange
        var readings = new List<MotionReading>();
        for (int i = 0; i < 10; i++)
        {
            readings.Add(new MotionReading
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Corridor,
                Timestamp = DateTime.UtcNow.AddMinutes(i),
                MotionDetected = i % 2 == 0,
            });
        }

        await _dbContext.MotionReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings()
            .Skip(5)
            .Take(3)
            .ToList();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetReadings_ShouldSupportProjectionToAnonymousType()
    {
        // Arrange
        var reading = new MotionReading
        {
            Id = Guid.NewGuid(),
            LocationId = LocationType.Garage,
            Timestamp = DateTime.UtcNow,
            MotionDetected = true,
        };

        await _dbContext.MotionReadings.AddAsync(reading, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings()
            .Select(x => new { x.LocationId, x.MotionDetected })
            .First();

        // Assert
        result.LocationId.Should().Be(LocationType.Garage);
        result.MotionDetected.Should().BeTrue();
    }
}
