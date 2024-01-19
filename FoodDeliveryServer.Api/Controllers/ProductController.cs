using FluentValidation;
using FoodDeliveryServer.Common.Dto.Auth;
using FoodDeliveryServer.Common.Dto.Error;
using FoodDeliveryServer.Common.Dto.Product;
using FoodDeliveryServer.Common.Enums;
using FoodDeliveryServer.Common.Exceptions;
using FoodDeliveryServer.Core.Interfaces;
using FoodDeliveryServer.Data.Models;
using FoodDeliveryServer.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FoodDeliveryServer.Azure.Servicebus;
using FoodDeliveryServer.Common.Dto.Store;

namespace FoodDeliveryServer.Api.Controllers
{
    [ApiController]
    [Route("/api/products")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IServiceMessage _serviceMessage;
        private string[] _messages;


        public ProductController(IProductService productService,IServiceMessage serviceMessage)
        {
            _productService = productService;
            _serviceMessage = serviceMessage;
            _messages = new string[] { };

        }

        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] long? storeId)
        {
            List<GetProductResponseDto> responseDto = await _productService.GetProducts(storeId ?? null);

            return Ok(responseDto);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(long id)
        {
            GetProductResponseDto responseDto;

            try
            {
                responseDto = await _productService.GetProduct(id);
            }
            catch (ResourceNotFoundException ex)
            {
                return NotFound(new ErrorResponseDto() { Message = ex.Message });
            }

            return Ok(responseDto);
        }

        [HttpPost]
        [Authorize(Roles = "FoodDeliveryApi.Partner")]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequestDto requestDto)
        {
            //Claim? idClaim = User.Claims.FirstOrDefault(x => x.Type == "UserId");
            //long userId = long.Parse(idClaim!.Value);
            long userId = 2;

            CreateProductResponseDto responseDto;

            try
            {
                responseDto = await _productService.CreateProduct(userId, requestDto);
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

        [HttpPut("{id}")]
        [Authorize(Roles = "Partner", Policy = "VerifiedPartner")]
        public async Task<IActionResult> UpdateProduct(long id, [FromBody] UpdateProductRequestDto requestDto)
        {
            Claim? idClaim = User.Claims.FirstOrDefault(x => x.Type == "UserId");
            long userId = long.Parse(idClaim!.Value);

            UpdateProductResponseDto responseDto;

            try
            {
                responseDto = await _productService.UpdateProduct(id, userId, requestDto);
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

            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Partner", Policy = "VerifiedPartner")]
        public async Task<IActionResult> DeleteProduct(long id)
        {
            Claim? idClaim = User.Claims.FirstOrDefault(x => x.Type == "UserId");
            long userId = long.Parse(idClaim!.Value);

            DeleteProductResponseDto responseDto;

            try
            {
                responseDto = await _productService.DeleteProduct(id, userId);
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

        [HttpPut("{id}/image")]
        [Authorize(Roles = "Partner", Policy = "VerifiedPartner")]
        public async Task<IActionResult> UploadImage(long id, [FromForm] IFormFile image)
        {
            Claim? idClaim = User.Claims.FirstOrDefault(x => x.Type == "UserId");
            long userId = long.Parse(idClaim!.Value);

            ImageResponseDto responseDto;

            try
            {
                string imageName = image.FileName;

                using Stream imageStream = image.OpenReadStream();

                responseDto = await _productService.UploadImage(id, userId, imageStream, imageName);
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

        private Task AddInfosFromInsertToMessages(CreateProductResponseDto storeRequestDto)
        {

            _messages = _messages.Append($"Product").ToArray();

            //Add the Id
            _messages = _messages.Append($"{storeRequestDto.Id}").ToArray();
            //add name
            _messages = _messages.Append(storeRequestDto.Name).ToArray();
            //add Description
            _messages = _messages.Append(storeRequestDto.Description).ToArray();
            //add Price
            _messages = _messages = _messages.Append($"{storeRequestDto.Price}").ToArray();
            //add Quantity
            _messages = _messages = _messages.Append($"{storeRequestDto.Quantity}").ToArray();
            //contact
            _messages = _messages.Append($"{storeRequestDto.StoreId}").ToArray();

            return Task.CompletedTask;
        }
    }
}
