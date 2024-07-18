using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;
using StargateAPI.Business.Commands;
using MediatR;
using StargateAPI.Business.Queries;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Data;
using System.Data.Common;
using StargateAPI.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;

namespace stargate.tests;

public class CreateAstronautDutyTests
{
    private StargateContext _context;

    public CreateAstronautDutyTests()
    {
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<StargateContext>()
            .UseSqlite(connection)
            .Options;
       
        _context = new StargateContext(options);
        _context.Database.EnsureCreated();
    }

    private void Dispose()
    {
        _context.Dispose();
    }

    /*
     * 1. In astronaut detail, John Doe has new role called Muse
     * 2. In astronaut duty, John Doe end date at commander is set at the day before start date
     * 3. Create a new astronaut duty with with the request info
     */
    [Fact]
    public async Task Handle_CreateNewAstronautDutyAndUpdateAstronautDetail()
    {
        // Arrange
        var hanlder = new CreateAstronautDutyHandler(_context);

        // Set up test data
        var request = new CreateAstronautDuty
        {
            Name = "John Doe",
            DutyTitle = "Muse",
            Rank = "1LT",
            DutyStartDate = new DateTime(2025, 1, 1)
        };

        // await _context.People.AddAsync(person);
        await _context.SaveChangesAsync();

        // Act
        var result = await hanlder.Handle(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(request.DutyTitle, result.DutyTitle);
        Assert.Equal(request.DutyStartDate, result.DutyStartDate);
    }

    [Fact]
    public async Task Handle_CreateNewAstronautDutyAndCreateNewAstronautDetail()
    {
        // Arrange
        var hanlder = new CreateAstronautDutyHandler(_context);

        // Set up test data
        var request = new CreateAstronautDuty
        {
            Name = "Jane Doe",
            DutyTitle = "Engineer",
            Rank = "1LT",
            DutyStartDate = new DateTime(2020, 1, 1)
        };

        // await _context.People.AddAsync(person);
        await _context.SaveChangesAsync();

        // Act
        var result = await hanlder.Handle(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

}
