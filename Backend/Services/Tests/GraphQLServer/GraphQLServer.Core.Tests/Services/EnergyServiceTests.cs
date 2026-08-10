using FluentAssertions;
using GraphQLServer.Core.Dto;
using GraphQLServer.Core.Services;
using GraphQLServer.Data;
using GraphQLServer.Data.Entities;
using MetersApp.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace GraphQLServer.Core.Tests.Services;

public class EnergyServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly EnergyService _service;

    public EnergyServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _service = new EnergyService(_dbContext);
    }

    [Fact]
    public async Task GetReadings_ShouldReturnQueryable()
    {
        // Act
        var result = await _service.GetReadings().ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IEnumerable<EnergyReadingDto>>();
    }

    [Fact]
    public async Task GetReadings_ShouldReturnAllReadings()
    {
        // Arrange
        var readings = new List<EnergyReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.LivingRoom,
                Timestamp = DateTime.UtcNow,
                Energy = 1250.5f,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Kitchen,
                Timestamp = DateTime.UtcNow.AddMinutes(-1),
                Energy = 850.25f,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Office,
                Timestamp = DateTime.UtcNow.AddMinutes(-2),
                Energy = 2100f,
            },
        };

        await _dbContext.EnergyReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
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

        var reading = new EnergyReading
        {
            Id = id,
            LocationId = LocationType.Bedroom,
            Timestamp = timestamp,
            Energy = 500.75f,
        };

        await _dbContext.EnergyReadings.AddAsync(reading, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings().First();

        // Assert
        result.Id.Should().Be(id);
        result.LocationId.Should().Be(LocationType.Bedroom);
        result.Timestamp.Should().Be(timestamp);
        result.Energy.Should().Be(500.75f);
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
    public async Task GetReadings_ShouldSupportFilteringByEnergyLevel()
    {
        // Arrange
        var readings = new List<EnergyReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.LivingRoom,
                Timestamp = DateTime.UtcNow,
                Energy = 500f,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Kitchen,
                Timestamp = DateTime.UtcNow,
                Energy = 1500f,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Office,
                Timestamp = DateTime.UtcNow,
                Energy = 2500f,
            },
        };

        await _dbContext.EnergyReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings()
            .Where(x => x.Energy > 1000)
            .ToList();

        // Assert
        result.Should().HaveCount(2);
        result.All(x => x.Energy > 1000).Should().BeTrue();
    }

    [Fact]
    public async Task GetReadings_ShouldSupportFilteringByLocation()
    {
        // Arrange
        var readings = new List<EnergyReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Garage,
                Timestamp = DateTime.UtcNow,
                Energy = 800f,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Corridor,
                Timestamp = DateTime.UtcNow,
                Energy = 400f,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Garage,
                Timestamp = DateTime.UtcNow,
                Energy = 1200f,
            },
        };

        await _dbContext.EnergyReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings()
            .Where(x => x.LocationId == LocationType.Garage)
            .ToList();

        // Assert
        result.Should().HaveCount(2);
        result.All(x => x.LocationId == LocationType.Garage).Should().BeTrue();
    }

    [Fact]
    public async Task GetReadings_ShouldSupportOrderingByEnergy()
    {
        // Arrange
        var readings = new List<EnergyReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.LivingRoom,
                Timestamp = DateTime.UtcNow,
                Energy = 1000f,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Kitchen,
                Timestamp = DateTime.UtcNow,
                Energy = 3000f,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Bedroom,
                Timestamp = DateTime.UtcNow,
                Energy = 500f,
            },
        };

        await _dbContext.EnergyReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings()
            .OrderByDescending(x => x.Energy)
            .ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].LocationId.Should().Be(LocationType.Kitchen);
        result[1].LocationId.Should().Be(LocationType.LivingRoom);
        result[2].LocationId.Should().Be(LocationType.Bedroom);
    }

    [Fact]
    public async Task GetReadings_ShouldSupportTimeRangeFiltering()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var readings = new List<EnergyReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.LivingRoom,
                Timestamp = baseTime.AddHours(-2),
                Energy = 500f,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Kitchen,
                Timestamp = baseTime.AddHours(-1),
                Energy = 800f,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Office,
                Timestamp = baseTime,
                Energy = 1200f,
            },
        };

        await _dbContext.EnergyReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Get readings from last hour
        var result = _service.GetReadings()
            .Where(x => x.Timestamp >= baseTime.AddHours(-1))
            .ToList();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetReadings_ShouldSupportAggregation()
    {
        // Arrange
        var readings = new List<EnergyReading>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.LivingRoom,
                Timestamp = DateTime.UtcNow,
                Energy = 100f,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.LivingRoom,
                Timestamp = DateTime.UtcNow,
                Energy = 200f,
            },
            new()
            {
                Id = Guid.NewGuid(),
                LocationId = LocationType.Kitchen,
                Timestamp = DateTime.UtcNow,
                Energy = 300f,
            },
        };

        await _dbContext.EnergyReadings.AddRangeAsync(readings, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var totalEnergy = _service.GetReadings()
            .Where(x => x.LocationId == LocationType.LivingRoom)
            .Sum(x => x.Energy);

        // Assert
        totalEnergy.Should().Be(300f);
    }

    [Fact]
    public async Task GetReadings_ShouldSupportProjection()
    {
        // Arrange
        var reading = new EnergyReading
        {
            Id = Guid.NewGuid(),
            LocationId = LocationType.Office,
            Timestamp = DateTime.UtcNow,
            Energy = 1500f,
        };

        await _dbContext.EnergyReadings.AddAsync(reading, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = _service.GetReadings()
            .Select(x => new { x.LocationId, EnergyInKwh = x.Energy / 1000 })
            .First();

        // Assert
        result.LocationId.Should().Be(LocationType.Office);
        result.EnergyInKwh.Should().Be(1.5f);
    }
}
