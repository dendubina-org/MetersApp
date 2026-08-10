using FluentAssertions;
using GraphQLServer.Core.Dto;
using GraphQLServer.Core.Services;
using GraphQLServer.Data;
using GraphQLServer.Data.Entities;
using MetersApp.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace GraphQLServer.Core.Tests.Services;

public class AirQualityServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly AirQualityService _service;

    public AirQualityServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _service = new AirQualityService(_dbContext);
    }

    [Fact]
    public async Task GetReadings_ShouldReturnQueryable()
    {
        // Act
        var result = await _service.GetReadings().ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IEnumerable<AirQualityReadingDto>>();
    }

    [Fact]
    public async Task GetReadings_ShouldReturnAllReadings()
    {
        // Arrange
        var readings = new List<AirQualityReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.LivingRoom,
                Timestamp = DateTime.UtcNow,
                Co2 = 450,
                Pm25 = 35,
                Humidity = 60,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Kitchen,
                Timestamp = DateTime.UtcNow.AddMinutes(-1),
                Co2 = 500,
                Pm25 = 40,
                Humidity = 65,
            },
        };

        await _dbContext.AirQualityReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings().ToList();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetReadings_ShouldProjectCorrectFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var reading = new AirQualityReading
        {
            Id = id,
            LocationId = LocationType.Bedroom,
            Timestamp = timestamp,
            Co2 = 420,
            Pm25 = 25,
            Humidity = 55,
        };

        await _dbContext.AirQualityReadings.AddAsync(reading, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings().First();

        // Assert
        result.Id.Should().Be(id);
        result.LocationId.Should().Be(LocationType.Bedroom);
        result.Timestamp.Should().Be(timestamp);
        result.Co2.Should().Be(420);
        result.Pm25.Should().Be(25);
        result.Humidity.Should().Be(55);
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
    public async Task GetReadings_ShouldSupportFilteringByCo2Level()
    {
        // Arrange
        var readings = new List<AirQualityReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.LivingRoom,
                Timestamp = DateTime.UtcNow,
                Co2 = 400,
                Pm25 = 30,
                Humidity = 50,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Kitchen,
                Timestamp = DateTime.UtcNow,
                Co2 = 600,
                Pm25 = 45,
                Humidity = 65,
            },
        };

        await _dbContext.AirQualityReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings()
            .Where(x => x.Co2 > 500)
            .ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].LocationId.Should().Be(LocationType.Kitchen);
    }

    [Fact]
    public async Task GetReadings_ShouldSupportFilteringByLocation()
    {
        // Arrange
        var readings = new List<AirQualityReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Office,
                Timestamp = DateTime.UtcNow,
                Co2 = 450,
                Pm25 = 35,
                Humidity = 60,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Kitchen,
                Timestamp = DateTime.UtcNow,
                Co2 = 500,
                Pm25 = 40,
                Humidity = 65,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Office,
                Timestamp = DateTime.UtcNow,
                Co2 = 480,
                Pm25 = 38,
                Humidity = 62,
            },
        };

        await _dbContext.AirQualityReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings()
            .Where(x => x.LocationId == LocationType.Office)
            .ToList();

        // Assert
        result.Should().HaveCount(2);
        result.All(x => x.LocationId == LocationType.Office).Should().BeTrue();
    }

    [Fact]
    public async Task GetReadings_ShouldSupportOrderingByHumidity()
    {
        // Arrange
        var readings = new List<AirQualityReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.LivingRoom,
                Timestamp = DateTime.UtcNow,
                Co2 = 450,
                Pm25 = 35,
                Humidity = 60,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Kitchen,
                Timestamp = DateTime.UtcNow,
                Co2 = 500,
                Pm25 = 40,
                Humidity = 80,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Bedroom,
                Timestamp = DateTime.UtcNow,
                Co2 = 420,
                Pm25 = 25,
                Humidity = 50,
            },
        };

        await _dbContext.AirQualityReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings()
            .OrderByDescending(x => x.Humidity)
            .ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].LocationId.Should().Be(LocationType.Kitchen);
        result[1].LocationId.Should().Be(LocationType.LivingRoom);
        result[2].LocationId.Should().Be(LocationType.Bedroom);
    }

    [Fact]
    public async Task GetReadings_ShouldSupportComplexFiltering()
    {
        // Arrange
        var readings = new List<AirQualityReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.LivingRoom,
                Timestamp = DateTime.UtcNow,
                Co2 = 450,
                Pm25 = 35,
                Humidity = 60,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Kitchen,
                Timestamp = DateTime.UtcNow,
                Co2 = 600,
                Pm25 = 55,
                Humidity = 70,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Bedroom,
                Timestamp = DateTime.UtcNow,
                Co2 = 550,
                Pm25 = 30,
                Humidity = 55,
            },
        };

        await _dbContext.AirQualityReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Find readings with high CO2 but acceptable PM2.5
        var result = _service.GetReadings()
            .Where(x => x.Co2 > 500 && x.Pm25 < 40)
            .ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].LocationId.Should().Be(LocationType.Bedroom);
    }

    [Fact]
    public async Task GetReadings_ShouldNotTrackEntities()
    {
        // Arrange
        var reading = new AirQualityReading
        {
            Id = Guid.NewGuid(),
            LocationId = LocationType.Garage,
            Timestamp = DateTime.UtcNow,
            Co2 = 400,
            Pm25 = 30,
            Humidity = 50,
        };

        await _dbContext.AirQualityReadings.AddAsync(reading, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Get readings and modify (should not affect DB due to AsNoTracking)
        var dto = _service.GetReadings().First();
        dto.Co2 = 999;

        // Reload from database
        var dbReading = await _dbContext.AirQualityReadings
            .FirstOrDefaultAsync(r => r.Id == reading.Id, TestContext.Current.CancellationToken);

        // Assert
        dbReading.Should().NotBeNull();
        dbReading.Co2.Should().Be(400); // Original value unchanged
    }
}
