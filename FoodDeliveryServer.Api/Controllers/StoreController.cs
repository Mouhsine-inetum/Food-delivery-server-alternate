using FluentValidation;
using FoodDeliveryServer.Azure.Servicebus;
using FoodDeliveryServer.Common.Dto.Auth;
using FoodDeliveryServer.Common.Dto.Error;
using FoodDeliveryServer.Common.Dto.Store;
using FoodDeliveryServer.Common.Enums;
using FoodDeliveryServer.Common.Exceptions;
using FoodDeliveryServer.Core.Interfaces;
using FoodDeliveryServer.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection.Metadata.Ecma335;
using System.Security.Claims;

namespace FoodDeliveryServer.Api.Controllers
{
    [ApiController]
    [Route("/api/stores")]
    public class StoreController : ControllerBase
    {
        private readonly IStoreService _storeService;
        private readonly IServiceMessage _serviceMessage;
        private  string[] _messages;

        public StoreController(IStoreService storeService, IServiceMessage serviceMessage)
        {
            _serviceMessage = serviceMessage;
            _storeService = storeService;
            _messages = new string[] {};
        }

        [HttpGet]
        public async Task<IActionResult> GetStores([FromQuery] long? partnerId = null, double? latitude = null, double? longitude = null)
        {
            List<GetStoreResponseDto> responseDto = await _storeService.GetStores(partnerId ?? null, latitude ?? null, longitude ?? null);

            return Ok(responseDto);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetStore(long id)
        {
            GetStoreResponseDto responseDto;

            try
            {
                responseDto = await _storeService.GetStore(id);
            }
            catch (ResourceNotFoundException ex)
            {
                return NotFound(new ErrorResponseDto() { Message = ex.Message });
            }

            return Ok(responseDto);
        }

        [HttpPost]
        [Authorize(Roles = "FoodDeliveryApi.Partner")]
        public async Task<IActionResult> CreateStore([FromBody] CreateStoreRequestDto requestDto)
        {
            //Claim? idClaim = User.Claims.FirstOrDefault(x => x.Type == "UserId");
            long userId = 2; //long.Parse(idClaim!.Value);

            CreateStoreResponseDto responseDto;

            try
            {
                responseDto = await _storeService.CreateStore(userId, requestDto);
                await AddInfosFromInsertToMessages(responseDto);
                await _serviceMessage.SendMessageServiceBus(_messages);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new ErrorResponseDto()
                {
                    Message = "One or more validation errors occurred. See the 'Errors' for details.",
                    Errors = ex.Errors.Select(err => err.ErrorMessage).ToList()
                });
            }
            catch (InvalidImageException ex)
            {
                return BadRequest(new ErrorResponseDto() { Message = ex.Message });
            }
            catch (InvalidTopologyException ex)
            {
                return BadRequest(new ErrorResponseDto() { Message = ex.Message });
            }

            return Ok(responseDto);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Partner", Policy = "VerifiedPartner")]
        public async Task<IActionResult> UpdateStore(long id, [FromBody] UpdateStoreRequestDto requestDto)
        {
            Claim? idClaim = User.Claims.FirstOrDefault(x => x.Type == "UserId");
            long userId = long.Parse(idClaim!.Value);

            UpdateStoreResponseDto responseDto;

            try
            {
                responseDto = await _storeService.UpdateStore(id, userId, requestDto);
            }
            catch (ResourceNotFoundException ex)
            {
                return NotFound(new ErrorResponseDto() { Message = ex.Message });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new ErrorResponseDto()
                {
                    Message = "One or more validation errors occurred. See the 'Errors' for details.",
                    Errors = ex.Errors.Select(err => err.ErrorMessage).ToList()
                });
            }
            catch (ActionNotAllowedException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponseDto()
                {
                    Message = ex.Message
                });
            }
            catch (InvalidTopologyException ex)
            {
                return BadRequest(new ErrorResponseDto() { Message = ex.Message });
            }

            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteStore(long id)
        {
            DeleteStoreResponseDto responseDto;

            try
            {
                responseDto = await _storeService.DeleteStore(id);
            }
            catch (ResourceNotFoundException ex)
            {
                return NotFound(new ErrorResponseDto() { Message = ex.Message });
            }

            return Ok(responseDto);
        }

        [HttpPut("{id}/image")]
        [Authorize(Roles = "Partner", Policy = "VerifiedPartner")]
        public async Task<IActionResult> UploadImage(long id, [FromForm] IFormFile image)
        {
            Claim? idClaim = User.Claims.FirstOrDefault(x => x.Type == "UserId");
            long userId = long.Parse(idClaim!.Value);

            Claim? roleClaim = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role);
            UserType userType = (UserType)Enum.Parse(typeof(UserType), roleClaim!.Value);

            ImageResponseDto responseDto;

            try
            {
                string imageName = image.FileName;

                using Stream imageStream = image.OpenReadStream();

                responseDto = await _storeService.UploadImage(id, userId, imageStream, imageName);
            }
            catch (InvalidImageException ex)
            {
                return BadRequest(new ErrorResponseDto() { Message = ex.Message });
            }
            catch (ResourceNotFoundException ex)
            {
                return NotFound(new ErrorResponseDto() { Message = ex.Message });
            }
            catch (ActionNotAllowedException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponseDto()
                {
                    Message = ex.Message
                });
            }

            return Ok(responseDto);
        }

        private Task AddInfosFromInsertToMessages(CreateStoreResponseDto storeRequestDto)
        {
            _messages = _messages.Append($"Store").ToArray();

            _messages = _messages.Append($"Store created").ToArray();
            //add name
            _messages = _messages.Append($"Store name : {storeRequestDto.Name}").ToArray();
            //add partner
           _messages = _messages.Append($"Description : {storeRequestDto.Description}").ToArray();
            //add city
            _messages=_messages = _messages.Append($"At : {storeRequestDto.City}").ToArray();
            //contact
            _messages = _messages.Append($"Who to contact : {storeRequestDto.Phone}").ToArray();
            return Task.CompletedTask;
        }
    }
}
