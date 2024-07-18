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


namespace stargate.tests;


public class AstronautDutyControllersTest
{
    private readonly Mock<IMediator> _mediator;
    private readonly AstronautDutyController _controller;

    public AstronautDutyControllersTest()
    {
        _mediator = new Mock<IMediator>();
        _controller = new AstronautDutyController(_mediator.Object);
    }

    [Fact]
    public async Task GetAstronautDutiesByName_ReturnSuccess()
    {
        // Arrange
        var name = "John Doe";
        var response = new BaseResponse { Success = true };

        _mediator.Setup(m => m.Send(It.IsAny<GetPersonByName>(), default));

        // Act
        var result = await _controller.GetAstronautDutiesByName(name);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnResponse = Assert.IsType<BaseResponse>(okResult.Value);
        Assert.True(returnResponse.Success);
    }

    [Fact]
    public async Task GetAstronautDutiesByName_ReturnsInternalServerError_OnException()
    {
        // Arrange
        var name = "John Doe";
        var exceptionMessage = "Some error";
        _mediator.Setup(m => m.Send(It.IsAny<GetPersonByName>(), default))
                     .ThrowsAsync(new System.Exception(exceptionMessage));

        // Act
        var result = await _controller.GetAstronautDutiesByName(name);

        // Assert
        var internalServerErrorResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, internalServerErrorResult.StatusCode);
        var returnResponse = Assert.IsType<BaseResponse>(internalServerErrorResult.Value);
        Assert.False(returnResponse.Success);
        Assert.Equal(exceptionMessage, returnResponse.Message);
    }
}