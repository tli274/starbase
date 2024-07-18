using Dapper;
using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;
using System;
using System.Net;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace StargateAPI.Business.Commands
{
    public class CreateAstronautDuty : IRequest<CreateAstronautDutyResult>
    {
        public required string Name { get; set; }

        public required string Rank { get; set; }

        public required string DutyTitle { get; set; }

        public DateTime DutyStartDate { get; set; }
    }

    public class CreateAstronautDutyPreProcessor : IRequestPreProcessor<CreateAstronautDuty>
    {
        private readonly StargateContext _context;

        public CreateAstronautDutyPreProcessor(StargateContext context)
        {
            _context = context;
        }

        public Task Process(CreateAstronautDuty request, CancellationToken cancellationToken)
        {
            var person = _context.People.AsNoTracking().FirstOrDefault(z => z.Name == request.Name);

            if (person is null) throw new BadHttpRequestException("Bad Request");

            var verifyNoPreviousDuty = _context.AstronautDuties.FirstOrDefault(z => z.DutyTitle == request.DutyTitle && z.DutyStartDate == request.DutyStartDate);

            if (verifyNoPreviousDuty is not null) throw new BadHttpRequestException("Bad Request");

            return Task.CompletedTask;
        }
    }

    public class CreateAstronautDutyHandler : IRequestHandler<CreateAstronautDuty, CreateAstronautDutyResult>
    {
        private readonly StargateContext _context;

        public CreateAstronautDutyHandler(StargateContext context)
        {
            _context = context;
        }
        public async Task<CreateAstronautDutyResult> Handle(CreateAstronautDuty request, CancellationToken cancellationToken)
        {
            using (var transation = await _context.Database.BeginTransactionAsync(cancellationToken))
            {
                try
                {
                    var person = await GetPersonByname(request.Name);

                    var astronautDetail = await GetAstronautDetailByPersonId(person.Id);

                    var prevAstronautDetail = await GetAstronautDetailByDutyTitle(request.DutyTitle);

                    // Work around for InvalidOperationException  
                    if (astronautDetail != null && prevAstronautDetail != null && astronautDetail.Id == prevAstronautDetail.Id)
                    {
                        await UpdateExistingAstronautDetail(prevAstronautDetail, request);
                    }
                    else
                    {
                        // New Astronaut with New Duties
                        if (astronautDetail == null)
                        {
                            await CreateNewAstronautDetail(person.Id, request);
                        }
                        else // Existing Astronaut
                        {
                            await UpdateExistingAstronautDetail(astronautDetail, request);
                        }
                        if (prevAstronautDetail != null)
                        {
                            await RemovePrevDutyAstronautDetail(prevAstronautDetail);
                        }
                    }


                    var astronautDuty = await GetAstronautDutyByPersonId(person.Id);
                    var prevAstronautDuty = await GetAstronautDutyByDutyTitle(request.DutyTitle);

                    // Update previous person astronaut duty
                    // Work around for InvalidOperationException
                    if (astronautDuty != null && prevAstronautDuty != null && astronautDuty.Id == prevAstronautDuty.Id)
                    {
                        await UpdateExistingAstronautDuty(astronautDuty, request);
                    }
                    else
                    {
                        if (astronautDuty != null) // Astronaut had a previous role
                        {
                            await UpdateExistingAstronautDuty(astronautDuty, request);
                        }
                        if (prevAstronautDuty != null) // Duty has been assigned before
                        {
                            await UpdateExistingAstronautDuty(prevAstronautDuty, request);
                        }
                    }

                    // Add new duties
                    var newAstronautDuty = await AddNewAstronautDuty(person.Id, request);

                    // Commit changes
                    await _context.SaveChangesAsync();
                    await transation.CommitAsync(cancellationToken);

                    return new CreateAstronautDutyResult()
                    {
                        Id = newAstronautDuty.Id
                    };
                }
                catch (Exception ex)
                {
                    return new CreateAstronautDutyResult()
                    {
                        Success = false,
                        Message = ex.Message
                    };
                }
            }
        }

        private async Task<Person> GetPersonByname(string name)
        {
            var query = $"SELECT * FROM [Person] WHERE \'{name}\' = Name";
            return await _context.Connection.QueryFirstOrDefaultAsync<Person>(query);
        }

        private async Task<AstronautDetail> GetAstronautDetailByPersonId(int personId)
        {
            var query = $"SELECT * FROM [AstronautDetail] WHERE {personId} = PersonId";
            return await _context.Connection.QueryFirstOrDefaultAsync<AstronautDetail>(query);
        }

        private async Task<AstronautDetail> GetAstronautDetailByDutyTitle(string dutyTitle)
        {
            var query = $"SELECT * FROM [AstronautDetail] WHERE \'{dutyTitle}\'  = CurrentDutyTitle";
            return await _context.Connection.QueryFirstOrDefaultAsync<AstronautDetail>(query);
        }

        private async Task CreateNewAstronautDetail(int personId, CreateAstronautDuty request)
        {
            var astronautDetail = new AstronautDetail
            {
                PersonId = personId,
                CurrentDutyTitle = request.DutyTitle,
                CurrentRank = request.Rank,
                CareerStartDate = request.DutyStartDate.Date,
                CareerEndDate = request.DutyTitle == "RETIRED" ? request.DutyStartDate.Date : (DateTime?)null
            };
            await _context.AstronautDetails.AddAsync(astronautDetail);
        }

        private async Task UpdateExistingAstronautDetail(AstronautDetail astronautDetail, CreateAstronautDuty request)
        {
            astronautDetail.CurrentDutyTitle = request.DutyTitle;
            astronautDetail.CurrentRank = request.Rank;
            if (request.DutyTitle == "RETIRED")
            {
                astronautDetail.CareerEndDate = request.DutyStartDate.AddDays(-1).Date;
            }
            _context.AstronautDetails.Update(astronautDetail);
        }

        private async Task RemovePrevDutyAstronautDetail(AstronautDetail astronautDetail)
        {
            astronautDetail.CurrentDutyTitle = "TRANSITION";
            _context.AstronautDetails.Update(astronautDetail);
        }

        private async Task<AstronautDuty> GetAstronautDutyByPersonId(int id)
        {
            var query = $"SELECT * FROM [AstronautDuty] WHERE {id} = PersonId Order By DutyStartDate Desc";
            return await _context.Connection.QueryFirstOrDefaultAsync<AstronautDuty>(query);
        }
        
        private async Task<AstronautDuty> GetAstronautDutyByDutyTitle(string duty)
        {
            var query = $"SELECT * FROM [AstronautDuty] WHERE \'{duty}\' = DutyTitle ORDER BY DutyStartDate Desc";
            return await _context.Connection.QueryFirstOrDefaultAsync<AstronautDuty>(query);
        }

        private async Task UpdateExistingAstronautDuty(AstronautDuty astronautDuty, CreateAstronautDuty request)
        {
            astronautDuty.DutyEndDate = request.DutyStartDate.AddDays(-1).Date;
            _context.AstronautDuties.Update(astronautDuty);
        }

        private async Task<AstronautDuty> AddNewAstronautDuty(int id,  CreateAstronautDuty request)
        {
            var newAstronautDuty = new AstronautDuty()
            {
                PersonId = id,
                Rank = request.Rank,
                DutyTitle = request.DutyTitle,
                DutyStartDate = request.DutyStartDate.Date,
                DutyEndDate = null
            };
            await _context.AstronautDuties.AddAsync(newAstronautDuty);
            return newAstronautDuty;
        }
    }

    public class CreateAstronautDutyResult : BaseResponse
    {
        public int? Id { get; set; }
    }
}
